using Google;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.OfferFlag.Repository;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.Payment_Gatway;
using System.Linq;

namespace SkillSwap_Platform.Services.AdminControls.OfferFlag
{
    public class OfferFlagService : IOfferFlagService
    {
        private readonly IOfferFlagRepository _flagRepo;
        private readonly IOfferRepository _offerRepo;
        private readonly ILogger<OfferFlagService> _log;
        private readonly SkillSwapDbContext _ctx;
        private readonly IUserServices _userService;         // to look up emails
        private readonly IEmailService _emailService;
        private readonly ISubscriptionService _subs;

        public OfferFlagService(
            IOfferFlagRepository repo,
            IOfferRepository offerRepo,
            ILogger<OfferFlagService> log,
            SkillSwapDbContext ctx,
            IUserServices userService,
            IEmailService emailService,
            ISubscriptionService subscription)
        {
            _flagRepo = repo;
            _offerRepo = offerRepo;
            _log = log;
            _ctx = ctx;
            _userService = userService;
            _emailService = emailService;
            _subs = subscription;
        }

        public async Task FlagOfferAsync(int offerId, int userId, string reason)
        {
            try
            {
                var flag = new TblOfferFlag
                {
                    OfferId = offerId,
                    FlaggedByUserId = userId,
                    FlaggedDate = DateTime.UtcNow,
                    Reason = reason
                };
                await _flagRepo.AddAsync(flag);

                // 2. Mark the offer itself as flagged
                var offer = await _offerRepo.GetByIdAsync(offerId);
                if (offer == null)
                    throw new KeyNotFoundException($"Swap offer {offerId} not found while flagging.");

                if (!offer.IsFlagged)   // avoid unnecessary updates
                {
                    offer.IsFlagged = true;
                    await _offerRepo.UpdateAsync(offer);
                }

                // notify the owner
                var owner = await _userService.GetUserByIdAsync(offer.UserId);
                if (owner != null)
                {
                    // --- compute prefix ---
                    var active = await _subs.GetActiveAsync(owner.UserId);
                    var (label, sla) = (active?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "24h SLA"),
                        "Growth" => ("Growth Support", "12h SLA"),
                        _ => ("Free Support", "72h SLA")
                    };

                    // --- flagged‐owner email ---
                    var subjectOfferReported = $"[{label} · {sla}] Notice: Your SkillSwap offer “{offer.Title}” has been reported";
                    var bodyOfferReported = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <!-- Header: Orange -->
      <tr><td style=""background:#FB8C00;padding:15px;color:#fff;text-align:center;font-size:18px;font-weight:bold;"">
        SkillSwap Moderation
      </td></tr>
      <!-- Body -->
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <h2 style=""margin-top:0;"">Hi {owner.FirstName},</h2>
        <p>A community member has flagged your swap offer:</p>
        <blockquote style=""color:#555;margin:0 0 1em 0;padding:0 1em;border-left:4px solid #ccc;"">
          “{offer.Title}”<br/>{reason}
        </blockquote>
        <p>Our moderation team will follow up within 24 hours. No action is required from you right now—simply reply if you’d like to provide context.</p>
      </td></tr>
      <!-- Footer -->
      <tr><td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
        — The SkillSwap Support Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>";
                    await _emailService.SendEmailAsync(owner.Email, subjectOfferReported, bodyOfferReported, isBodyHtml: true);

                    // acknowledge the flagger
                    var flagger = await _userService.GetUserByIdAsync(userId);
                    if (flagger != null)
                    {
                        var subjectThankFlagger = $"[{label} · {sla}] Thanks for reporting “{offer.Title}”";
                        var bodyThankFlagger = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <!-- Header: Green -->
      <tr><td style=""background:#388E3C;padding:15px;color:#fff;text-align:center;font-size:18px;font-weight:bold;"">
        SkillSwap Moderation
      </td></tr>
      <!-- Body -->
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <h2 style=""margin-top:0;"">Hi {flagger.FirstName},</h2>
        <p>Thanks for helping keep SkillSwap safe. We’ve received your report for:</p>
        <blockquote style=""margin:0 0 1em 0;padding:0 1em;border-left:4px solid #ccc;color:#555;"">
          “{offer.Title}”<br/>{reason}
        </blockquote>
        <p>We’ll let you know as soon as it’s been handled.</p>
      </td></tr>
      <!-- Footer -->
      <tr><td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
        — The SkillSwap Support Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>";
                        await _emailService.SendEmailAsync(flagger.Email, subjectThankFlagger, bodyThankFlagger, isBodyHtml: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error flagging offer {OfferId}", offerId);
                throw;
            }
        }

        public async Task<PagedResult<TblOfferFlag>> GetPendingFlagsAsync(int page, int pageSize)
        {
            var q = _ctx.TblOfferFlags
                        .Include(f => f.Offer)
                        .Include(f => f.FlaggedByUser)
                        .Where(f => !f.Offer.IsDeleted && f.AdminAction == null)
                        .OrderByDescending(f => f.FlaggedDate);

            var total = await q.CountAsync();
            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<TblOfferFlag>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<TblOfferFlag>> GetProcessedFlagsAsync(int page, int pageSize)
        {
            var q = _ctx.TblOfferFlags
                        .Include(f => f.Offer)
                        .Include(f => f.FlaggedByUser)
                        .Include(f => f.AdminUser)
                        .Where(f => f.AdminAction != null)
                        .OrderByDescending(f => f.AdminActionDate);

            var total = await q.CountAsync();
            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<TblOfferFlag>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task DismissFlagAsync(int flagId, int adminUserId, string reason)
        {
            // 1) load the flag
            var flag = await _flagRepo.GetByIdAsync(flagId)
                       ?? throw new KeyNotFoundException($"Flag {flagId} not found.");

            // 2) record admin action on that very row
            flag.AdminUserId = adminUserId;
            flag.AdminAction = "Dismiss";
            flag.AdminReason = reason;
            flag.AdminActionDate = DateTime.UtcNow;
            await _flagRepo.UpdateAsync(flag);

            // 3) clear the offer’s flagged bit if no pending flags remain
            var remaining = await _flagRepo.GetByOfferIdAsync(flag.OfferId);
            if (!remaining.Any(f => f.AdminAction == null))  // all have been actioned
            {
                var offer = await _offerRepo.GetByIdAsync(flag.OfferId)
                            ?? throw new KeyNotFoundException($"Offer {flag.OfferId} not found.");
                if (offer.IsFlagged)
                {
                    offer.IsFlagged = false;
                    await _offerRepo.UpdateAsync(offer);
                }
            }

            // email the owner
            var offerOwner = await _offerRepo.GetByIdAsync(flag.OfferId);
            var owner = await _userService.GetUserByIdAsync(offerOwner.UserId);
            if (owner != null)
            {
                var active = await _subs.GetActiveAsync(owner.UserId);
                var (label, sla) = (active?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "24h SLA"),
                    "Growth" => ("Growth Support", "12h SLA"),
                    _ => ("Free Support", "72h SLA")
                };

                var subjectOfferDismissed = $"[{label} · {sla}] Update: Report on your offer “{flag.Offer.Title}” has been dismissed";
                var bodyOfferDismissed = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <!-- Header: Gray -->
      <tr><td style=""background:#757575;padding:15px;color:#fff;text-align:center;font-size:18px;font-weight:bold;"">
        SkillSwap Moderation
      </td></tr>
      <!-- Body -->
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <h2 style=""margin-top:0;"">Hello {owner.FirstName},</h2>
        <p>Our team reviewed the report against your offer <strong>{flag.Offer.Title}</strong> and found no violation.</p>
        <p><strong>Reason:</strong></p>
        <blockquote style=""color:#555;margin:0 0 1em 0;padding:0 1em;border-left:4px solid #ccc;"">
          {reason}
        </blockquote>
        <p>No further action is needed—thanks for keeping SkillSwap great!</p>
      </td></tr>
      <!-- Footer -->
      <tr><td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
        — The SkillSwap Support Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>";
                await _emailService.SendEmailAsync(owner.Email, subjectOfferDismissed, bodyOfferDismissed, isBodyHtml: true);

            }

            // email the flagger
            var flagger = await _userService.GetUserByIdAsync(flag.FlaggedByUserId);
            if (flagger != null)
            {
                var activeF = await _subs.GetActiveAsync(flagger.UserId);
                var (lblF, slaF) = (activeF?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "24h SLA"),
                    "Growth" => ("Growth Support", "12h SLA"),
                    _ => ("Free Support", "72h SLA")
                };

                var subjectFlaggerDismiss = $"[{lblF} · {slaF}] Your report on “{flag.Offer.Title}” has been reviewed";
                var bodyFlaggerDismiss = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
      <!-- Header: Green -->
      <tr><td style=""background:#388E3C;padding:15px;color:#fff;text-align:center;font-size:18px;font-weight:bold;"">
        SkillSwap Moderation
      </td></tr>
      <!-- Body -->
      <tr><td style=""padding:20px;color:#333;line-height:1.5;"">
        <h2 style=""margin-top:0;"">Hi {flagger.FirstName},</h2>
        <p>We’ve completed our review of your report on <strong>{offerOwner.Title}</strong> and found no violations.</p>
        <p><strong>Moderator’s note:</strong></p>
        <blockquote style=""margin:0 0 1em 0;padding-left:1em;border-left:4px solid #ccc;"">
          {reason}
        </blockquote>
        <p>Thank you for helping maintain a respectful marketplace! We appreciate your participation.</p>
      </td></tr>
      <!-- Footer -->
      <tr><td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
        — The SkillSwap Support Team
      </td></tr>
    </table>
  </td></tr></table>
</body>
</html>";
                await _emailService.SendEmailAsync(flagger.Email, subjectFlaggerDismiss, bodyFlaggerDismiss, isBodyHtml: true);
            }
        }

        public async Task RemoveOfferAsync(int flagId, int adminUserId, string reason)
        {
            var flag = await _flagRepo.GetByIdAsync(flagId)
                       ?? throw new KeyNotFoundException($"Flag {flagId} not found.");

            // 1) mark the offer deleted
            var offer = await _offerRepo.GetByIdAsync(flag.OfferId)
                        ?? throw new KeyNotFoundException($"Offer {flag.OfferId} not found.");
            offer.IsDeleted = true;
            offer.DeletedDate = DateTime.UtcNow;
            await _offerRepo.UpdateAsync(offer);

            // 2) record admin action on the flag record
            flag.AdminUserId = adminUserId;
            flag.AdminAction = "RemoveOffer";
            flag.AdminReason = reason;
            flag.AdminActionDate = DateTime.UtcNow;
            await _flagRepo.UpdateAsync(flag);

            // email the owner
            var owner = await _userService.GetUserByIdAsync(offer.UserId);
            if (owner != null)
            {
                var active = await _subs.GetActiveAsync(owner.UserId);
                var (label, sla) = (active?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "24h SLA"),
                    "Growth" => ("Growth Support", "12h SLA"),
                    _ => ("Free Support", "72h SLA")
                };

                var subjectRemovedOwner = $"[{label} · {sla}] Action Taken: Your offer “{offer.Title}” has been removed";
                var bodyRemovedOwner = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header: Red -->
      <tr>
        <td style=""background:#D32F2F;padding:15px;text-align:center;color:#ffffff;font-size:18px;font-weight:bold;"">
          SkillSwap Moderation
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333333;line-height:1.5;"">
          <h2 style=""margin-top:0;"">Hi {owner.FirstName},</h2>
          <p>Following our review of the report on your swap offer, we have removed:</p>
          <blockquote style=""color:#555555;margin:0 0 1em 0;padding:0 1em;border-left:4px solid #ccc;"">
            “{offer.Title}”<br/>{reason}
          </blockquote>
          <p><strong>Token update:</strong> Any tokens locked in pending exchanges for this offer will be automatically released and refunded within 24 hours. If you still see held tokens or need them released sooner, please reply or <a href=""mailto:skillswap360@gmail.com"" style=""color:#00A88F;text-decoration:underline;"">contact support</a>.</p>
          <p>We understand this may be disappointing. If you believe this was a mistake or would like more information, simply reply to this email and we’ll assist you.</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          — The SkillSwap Support Team
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";
                await _emailService.SendEmailAsync(owner.Email, subjectRemovedOwner, bodyRemovedOwner, isBodyHtml: true);
            }

            // email the flagger
            var flagger = await _userService.GetUserByIdAsync(flag.FlaggedByUserId);
            if (flagger != null)
            {
                var activeF = await _subs.GetActiveAsync(flagger.UserId);
                var (lblF, slaF) = (activeF?.PlanName ?? "Free") switch
                {
                    "Plus" => ("Plus Support", "72h SLA"),
                    "Pro" => ("Pro Support", "24h SLA"),
                    "Growth" => ("Growth Support", "12h SLA"),
                    _ => ("Free Support", "72h SLA")
                };

                var subjectRemovedFlagger = $"[{lblF} · {slaF}] Thank you: “{offer.Title}” has been removed";
                var bodyRemovedFlagger = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header: Green (gratitude) -->
      <tr>
        <td style=""background:#388E3C;padding:15px;text-align:center;color:#ffffff;font-size:18px;font-weight:bold;"">
          SkillSwap Moderation
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333333;line-height:1.5;"">
          <h2 style=""margin-top:0;"">Hi {flagger.FirstName},</h2>
          <p>Thank you for your report on:</p>
          <blockquote style=""color:#555555;margin:0 0 1em 0;padding:0 1em;border-left:4px solid #ccc;"">
            “{offer.Title}”<br/>{reason}
          </blockquote>
          <p>We’ve reviewed and removed the offer. Any tokens previously locked will be refunded within 24 hours. If you still see any held tokens or need them released sooner, please reply or <a href=""mailto:skillswap360@gmail.com"" style=""color:#00A88F;text-decoration:underline;"">contact support</a>.</p>
          <p>Your help keeps our community strong and secure—thank you!</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          — The SkillSwap Support Team
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";
                await _emailService.SendEmailAsync(flagger.Email, subjectRemovedFlagger, bodyRemovedFlagger, isBodyHtml: true);
            }
        }

        public async Task<bool> HasPendingFlagAsync(int offerId, int flaggedByUserId)
        {
            try
            {
                return await _ctx.TblOfferFlags
                    .AnyAsync(f =>
                        f.OfferId == offerId
                        && f.FlaggedByUserId == flaggedByUserId
                        && f.AdminAction == null  // not yet processed
                    );
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Error checking pending flag for offer {OfferId} and user {UserId}",
                    offerId, flaggedByUserId);
                // on error, fail closed so UI shows “not flagged” (you can also default to true)
                return false;
            }
        }

        public async Task<PagedResult<FlaggedOfferSummary>> GetFlaggedOfferSummariesAsync(int page, int pageSize)
        {
            // 1) build the grouping
            var q = _ctx.TblOfferFlags
                .AsNoTracking()
                .GroupBy(f => f.OfferId)
                .Select(g => new FlaggedOfferSummary
                {
                    OfferId = g.Key,
                    TotalFlags = g.Count(),
                    Title = g.Select(f => f.Offer.Title).FirstOrDefault()!
                });

            // 2) Count and page
            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(x => x.TotalFlags)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<FlaggedOfferSummary>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<TblOfferFlag>> GetFlagsForOfferAsync(int offerId, int page, int pageSize)
        {
            var q = _ctx.TblOfferFlags
                        .AsNoTracking()
                        .Include(f => f.Offer)
                        .Include(f => f.FlaggedByUser)
                        .Include(f => f.AdminUser)
                        .Where(f => f.OfferId == offerId)
                        .OrderByDescending(f => f.FlaggedDate);

            var total = await q.CountAsync();
            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<TblOfferFlag>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<DashboardMetricsDto> GetDashboardMetricsAsync(
    DateTime periodStart, DateTime periodEnd,
    int mostFlaggedTake = 5,
    int recentActionsTake = 10)
        {
            // 1) Simple counts
            var totalOffers = await _ctx.TblOffers.CountAsync(o => !o.IsDeleted);
            var flaggedOffers = await _ctx.TblOffers.CountAsync(o => o.IsFlagged && !o.IsDeleted);
            var pendingFlags = await _ctx.TblOfferFlags.CountAsync(f => f.AdminAction == null);
            var resolvedFlags = await _ctx.TblOfferFlags.CountAsync(f => f.AdminAction != null);

            // 2) Flags‐by‐day: group by Y/M/D in SQL, then project
            var flagsByDayRaw = await _ctx.TblOfferFlags
                .Where(f => f.FlaggedDate >= periodStart && f.FlaggedDate <= periodEnd)
                .GroupBy(f => new { f.FlaggedDate.Year, f.FlaggedDate.Month, f.FlaggedDate.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
                .ToListAsync();

            var flagTrends = flagsByDayRaw
                .Select(x => new DateCount(new DateTime(x.Year, x.Month, x.Day), x.Count))
                .OrderBy(d => d.Date)
                .ToList();

            // 3) Resolution breakdown
            var resolutionBreakdown = await _ctx.TblOfferFlags
                .Where(f => f.AdminAction != null)
                .GroupBy(f => f.AdminAction!)
                .Select(g => new ActionCount(g.Key, g.Count()))
                .ToListAsync();

            // 4) Most-flagged offers
            var mostFlaggedOffers = await _ctx.TblOfferFlags
                .Where(f => !f.Offer.IsDeleted)
                .GroupBy(f => f.OfferId)
                .Select(g => new { OfferId = g.Key, TotalFlags = g.Count() })
                .OrderByDescending(x => x.TotalFlags)
                .Take(mostFlaggedTake)
                .Join(
                    _ctx.TblOffers,
                    grp => grp.OfferId,
                    o => o.OfferId,
                    (grp, o) => new FlaggedOffersSummary(
                        o.OfferId,
                        o.Title,
                        grp.TotalFlags,
                        o.Portfolio
                    )
                )
                .ToListAsync();

            // 5) Recent admin actions
            var recentActions = await _ctx.TblOfferFlags
                .Where(f => f.AdminAction != null)
                .OrderByDescending(f => f.AdminActionDate)
                .Take(recentActionsTake)
                .Select(f => new RecentActionDto(
                    f.AdminAction!,
                    f.AdminUser!.FirstName,
                    f.Offer!.Title,
                    f.AdminActionDate.Value))
                .ToListAsync();

            // Assemble and return
            return new DashboardMetricsDto
            {
                TotalOffers = totalOffers,
                FlaggedOffers = flaggedOffers,
                PendingFlags = pendingFlags,
                ResolvedFlags = resolvedFlags,
                FlagTrends = flagTrends,
                ResolutionBreakdown = resolutionBreakdown,
                MostFlaggedOffers = mostFlaggedOffers,
                RecentActions = recentActions
            };
        }
    }
}
