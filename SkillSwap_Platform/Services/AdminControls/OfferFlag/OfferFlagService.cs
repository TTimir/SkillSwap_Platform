﻿using Google;
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
                    var subject = $"[{label} · {sla}] Notice: Your SkillSwap offer “{offer.Title}” has been reported";
                    var body = $@"
                        Hi {owner.FirstName},<br/><br/>

                        We wanted to let you know that a member of our community on our platform has flagged your swap offer “<strong>{offer.Title}</strong>”<br/>
                        for the following reason:<br/>
                        <blockquote style=""color:#555;margin:0 0 1em 0;padding:0 1em;border-left:3px solid #ccc;"">{reason}</blockquote>

                        Our moderation team is already reviewing this report to ensure everyone has a safe experience and will get back to you within 24 hours with any next steps.
                        No action is required from you right now, if you feel this was a mistake or wish to add any context, simply reply to this email.<br/><br/>

                        Thank you for being part of SkillSwap — we appreciate your contributions!<br/><br/>

                        Cheers,<br/>
                        <em>The SkillSwap Support Team</em>
                    ";
                    await _emailService.SendEmailAsync(owner.Email, subject, body);

                    // acknowledge the flagger
                    var flagger = await _userService.GetUserByIdAsync(userId);
                    if (flagger != null)
                    {
                        var subjectFlagger = $"[{label} · {sla}] Thanks for reporting “{offer.Title}”";
                        var bodyFlagger = $@"
                            Hi {flagger.FirstName},<br/><br/>

                            Thanks for helping keep SkillSwap safe. We’ve received your report for the swap offer<br/>
                            <strong>“{offer.Title}”</strong><br/><br/>
                            <strong>Your remark:</strong><br/>
                            <blockquote style=""margin:0;padding:0 1em;border-left:3px solid #ccc;color:#555;"">{reason}</blockquote>

                            Our moderation team will review it and take appropriate action. We’ll let you know as soon as it’s been handled.
                            Feel free to reply if you have more information to share.<br/><br/>

                            Thank you for helping keep SkillSwap safe!<br/>
                            <em>The SkillSwap Support Team</em>
                        ";
                        await _emailService.SendEmailAsync(flagger.Email, subjectFlagger, bodyFlagger);
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

                var subject = $"[{label} · {sla}] Update: Report on your offer “{flag.Offer.Title}” has been dismissed";
                var body = $@"
                    Hello {owner.FirstName},<br/><br/>

                    Thanks for helping keep SkillSwap a safe and welcoming place. Our team has reviewed of the report against your swap offer {flag.Offer.Title}. and found it does not violate our guidelines.<br/>
                    <strong>Outcome:</strong> We’ve dismissed the flag.<br/>
                    We’ve decided to <strong>dismiss</strong> the flag for this reason:<br/>
                    <blockquote style=""color:#555;margin:0 0 1em 0;padding:0 1em;border-left:3px solid #ccc;"">{reason}</blockquote>

                    No further action is needed on your part. Thanks for your understanding and for contributing to our community.<br/><br/>

                    Thank you for helping keep our community safe. If you still have concerns, just reply to this email.<br/><br/>

                    Best,<br/>
                    <em>The SkillSwap Support Team</em>
                ";
                await _emailService.SendEmailAsync(owner.Email, subject, body);
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

                var subject = $"[{lblF} · {slaF}] Your report on “{flag.Offer.Title}” has been reviewed";
                var body = $@"
                Hi {flagger.FirstName},<br/><br/>
                We’ve completed our review of the report you submitted for “<strong>{offerOwner.Title}</strong>” and found no violations.<br/>
                <strong>Moderator’s note:</strong><br/>
                <blockquote style=""border-left:3px solid #ccc; padding-left:1em;"">{reason}</blockquote>
                Thank you for helping us maintain a respectful marketplace. We appreciate your participation!<br/><br/>
                
                Sincerely,<br/>
                <em>The SkillSwap Support Team</em>
            ";
                await _emailService.SendEmailAsync(flagger.Email, subject, body);
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

                var subject = $"[{label} · {sla}] Action Taken: Your offer “{offer.Title}” has been removed";
                var body = $@"
                    Hi {owner.FirstName},<br/><br/>

                    Following our review of the report on your swap offer<br/> we wanted to let you know that your swap offer “<strong>{offer.Title}</strong>” has been removed by our moderation team.<br/>
                    our moderation team has removed it for the reason below:<br/>
                    <blockquote style=""color:#555;margin:0 0 1em 0;padding:0 1em;border-left:3px solid #ccc;"">{reason}</blockquote>

                    <strong>Token update:</strong>  
                    Any tokens locked in pending exchanges for this offer will be automatically
                    released and refunded to the appropriate accounts within 24 hours.  
                    If you or the other party still see tokens held or need them released sooner,
                    just contact to this email or <a href=""mailto:skillswap360@gmail.com"">contact our support team</a>.<br/><br/>

                    We understand this may be disappointing. If you believe this was in error or would like more information, simply reply to this email and we'll be happy to assist.<br/>

                    Thank you for your cooperation,<br/>
                    <em>The SkillSwap Support Team</em>
                ";
                await _emailService.SendEmailAsync(owner.Email, subject, body);
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

                var subject = $"[{lblF} · {slaF}] Thank you: “{offer.Title}” has been removed";
                var body = $@"
                    Hi {flagger.FirstName},<br/><br/>
                    Thank you for your report on “<strong>{offer.Title}</strong>”. We’ve reviewed it and removed the offer.<br/>
                    <strong>Moderator’s note:</strong><br/>
                    <blockquote style=""border-left:3px solid #ccc; padding-left:1em;"">{reason}</blockquote>

                    <strong>Token update:</strong>  
                    Any tokens locked in pending exchanges for this offer will be automatically
                    released and refunded to the appropriate accounts within 24 hours.  
                    If you still see any held tokens or need a faster refund,
                    simply contact to this email or <a href=""mailto:skillswap360@gmail.com"">contact our support team</a>.<br/><br/>

                    Your help keeps our community strong and secure!<br/><br/>

                    Warm regards,<br/>
                    <em>The SkillSwap Support Team</em>
                ";
                await _emailService.SendEmailAsync(flagger.Email, subject, body);
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
