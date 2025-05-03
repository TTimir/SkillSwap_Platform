using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.AdminControls.UserFlag.Repository;
using SkillSwap_Platform.Services.Email;

namespace SkillSwap_Platform.Services.AdminControls.UserFlag
{
    public class UserFlagService : IUserFlagService
    {
        private readonly IUserFlagRepository _repo;
        private readonly SkillSwapDbContext _ctx;
        private readonly ILogger<UserFlagService> _log;
        private readonly IUserServices _userService;
        private readonly IEmailService _emailService;

        public UserFlagService(
            IUserFlagRepository repo,
            SkillSwapDbContext ctx,
            ILogger<UserFlagService> log,
            IUserServices userService,
            IEmailService emailService)
        {
            _repo = repo;
            _ctx = ctx;
            _log = log;
            _userService = userService;
            _emailService = emailService;
        }

        public async Task FlagUserAsync(int flaggedUserId, int byUserId, string reason)
        {
            try
            {
                var flag = new TblUserFlag
                {
                    FlaggedUserId = flaggedUserId,
                    FlaggedByUserId = byUserId,
                    FlaggedDate = DateTime.UtcNow,
                    Reason = reason
                };
                await _repo.AddAsync(flag);

                var user = await _ctx.TblUsers.FindAsync(flaggedUserId);
                if (user == null)
                    throw new KeyNotFoundException($"User {flaggedUserId} not found.");

                if (!user.IsFlagged)
                {
                    user.IsFlagged = true;
                    _ctx.TblUsers.Update(user);
                    await _ctx.SaveChangesAsync();
                }

                try
                {
                    var reported = await _userService.GetUserByIdAsync(flaggedUserId);
                    if (reported != null)
                    {
                        var subject = "Your SkillSwap profile has been reported";
                        var body = $@"
                            Hello {reported.FirstName},<br/><br/>
                            We wanted to let you know that a member of our community has reported your profile for the following reason:<br/>
                            <blockquote style=""margin:0 0 1em;padding-left:1em;border-left:3px solid #ccc;"">
                                {reason}
                            </blockquote>
                            Our moderation team is reviewing this report. If you have any questions or would like to provide additional context, please reply to this email or contact us at 
                            <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.<br/><br/>
                            Thank you for being part of SkillSwap—we appreciate your contributions!<br/><br/>
                            — The SkillSwap Support Team";
                        await _emailService.SendEmailAsync(reported.Email, subject, body);
                    }
                }
                catch (Exception mailEx)
                {
                    _log.LogError(mailEx, "Failed to email reported user {UserId}", flaggedUserId);
                }

                // 4) Acknowledge the reporter
                try
                {
                    var reporter = await _userService.GetUserByIdAsync(byUserId);
                    if (reporter != null)
                    {
                        var subject = "Thanks for reporting a profile";
                        var body = $@"
                            Hi {reporter.FirstName},<br/><br/>
                            We’ve received your report against user <strong>{flaggedUserId}</strong> with the following details:<br/>
                            <blockquote style=""margin:0 0 1em;padding-left:1em;border-left:3px solid #ccc;"">
                                {reason}
                            </blockquote>
                            Our moderation team will review it and take appropriate action. We’ll let you know once the review is complete.<br/><br/>
                            Thanks for helping keep SkillSwap safe!<br/><br/>
                            — The SkillSwap Support Team";
                        await _emailService.SendEmailAsync(reporter.Email, subject, body);
                    }
                }
                catch (Exception mailEx)
                {
                    _log.LogError(mailEx, "Failed to email reporter {UserId}", byUserId);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error flagging user {UserId}", flaggedUserId);
                throw;
            }
        }

        public async Task<PagedResult<TblUserFlag>> GetPendingFlagsAsync(int page, int pageSize)
        {
            // 1) load everything (no SQL grouping)
            var allFlags = await _ctx.TblUserFlags
                .AsNoTracking()
                .Include(f => f.FlaggedByUser)
                .Include(f => f.FlaggedUser)
                .ToListAsync();

            // 2) group by user and only keep users whose *every* flag has AdminAction == null
            var pending = allFlags
                .GroupBy(f => f.FlaggedUserId)
                .Where(g => g.All(f => f.AdminAction == null))
                .SelectMany(g => g)
                .OrderByDescending(f => f.FlaggedDate)
                .ToList();

            // 3) page in memory
            var total = pending.Count;
            var items = pending
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<TblUserFlag>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task DismissFlagAsync(int flagId, int adminId, string adminReason)
        {
            // 1) load and update the flag
            var flag = await _repo.GetByIdAsync(flagId)
                       ?? throw new KeyNotFoundException($"Flag {flagId} not found.");
            flag.AdminUserId = adminId;
            flag.AdminAction = "Dismissflag";
            flag.AdminReason = adminReason;
            flag.AdminActionDate = DateTime.UtcNow;
            await _repo.UpdateAsync(flag);

            // 2) clear User.IsFlagged if no more pending flags
            var pending = (await _repo.GetByUserAsync(flag.FlaggedUserId))
                              .Where(f => f.AdminAction == null);
            if (!pending.Any())
            {
                var user = await _ctx.TblUsers.FindAsync(flag.FlaggedUserId);
                if (user != null && user.IsFlagged)
                {
                    user.IsFlagged = false;
                    _ctx.TblUsers.Update(user);
                    await _ctx.SaveChangesAsync();
                }
            }

            // 3) inform both parties that the flag was dismissed
            var reported = await _userService.GetUserByIdAsync(flag.FlaggedUserId);
            if (reported != null)
            {
                var subject = "Your profile report has been dismissed";
                var body = $@"
                    Hello {reported.FirstName},<br/><br/>
                    Our moderation team has reviewed the report filed against your profile and found no violation.<br/>
                    <strong>Moderator’s note:</strong><br/>
                    <blockquote style=""margin:0 0 1em;padding-left:1em;border-left:3px solid #ccc;"">{adminReason}</blockquote>
                    Thanks for being part of SkillSwap!<br/><br/>
                    — The SkillSwap Support Team";
                await _emailService.SendEmailAsync(reported.Email, subject, body);
            }

            var reporter = await _userService.GetUserByIdAsync(flag.FlaggedByUserId ?? 0);
            if (reporter != null)
            {
                var subject = "Thank you for your report";
                var body = $@"
            Hi {reporter.FirstName},<br/><br/>
            We’ve completed our review of the report you submitted and decided to dismiss it.<br/>
            <strong>Moderator’s note:</strong><br/>
            <blockquote style=""margin:0 0 1em;padding-left:1em;border-left:3px solid #ccc;"">{adminReason}</blockquote>
            Your help keeps SkillSwap safe—thank you!<br/><br/>
            — The SkillSwap Support Team";
                await _emailService.SendEmailAsync(reporter.Email, subject, body);
            }
        }

        public async Task RemoveUserAsync(int flagId, int adminId, string adminReason)
        {
            {
                // 1) Load the original flag so we know which user we're working on
                var original = await _repo.GetByIdAsync(flagId)
                               ?? throw new KeyNotFoundException($"Flag {flagId} not found.");

                // 2) How many warnings (Warn1/Warn2) have already been issued?
                var warnCount = await _ctx.TblUserFlags
                    .CountAsync(f =>
                        f.FlaggedUserId == original.FlaggedUserId
                     && (f.AdminAction == "Warn1" || f.AdminAction == "Warn2"));

                // 3) Fetch the reported user
                var reported = await _userService.GetUserByIdAsync(original.FlaggedUserId)
                             ?? throw new KeyNotFoundException($"User {original.FlaggedUserId} not found.");

                // Prepare the new entry (either warning or removal)
                var entry = new TblUserFlag
                {
                    FlaggedUserId = reported.UserId,
                    FlaggedByUserId = null,                     // convention: admin actions use 0/null here
                    FlaggedDate = DateTime.UtcNow,
                    Reason = adminReason,
                    AdminUserId = adminId,
                    AdminActionDate = DateTime.UtcNow,
                    AdminReason = adminReason
                };

                string subject, body;

                // 3) Issue warning or deactivate based on strikeCount
                if (warnCount < 2)
                {
                    // Strike 1 or 2 → insert Warn1 or Warn2
                    entry.AdminAction = warnCount == 0 ? "Warn1" : "Warn2";
                    
                    if (warnCount == 0)
                    {
                        subject = "Notice: A report against your profile was reviewed";
                        body = $@"
                        Hello {reported.FirstName},<br/><br/>

                        Our moderation team reviewed a report against your profile and found no violation this time.<br/>
                        <strong>Moderator’s note:</strong><br/>
                        <blockquote style=""margin:0 0 1em;padding-left:1em;border-left:3px solid #ccc;"">
                            {adminReason}
                        </blockquote>

                        This is an official warning—no action is required now, but please ensure your future behavior aligns with our 
                        <a href=""/CommunityGuidelines"">Community Guidelines</a>.<br/><br/>

                        Note: Even though this report was dismissed, your profile remains under observation. Continued reports may lead 
                        to stricter action.<br/><br/>

                        If you have any questions or believe there’s a misunderstanding, just reply to this email or contact us at 
                        <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.<br/><br/>

                        Thank you for being part of SkillSwap!<br/><br/>
                        — The SkillSwap Support Team";
                    }
                    else
                    {
                        subject = "Final Warning: Another report against your profile was reviewed";
                        body = $@"
                        Hello {reported.FirstName},<br/><br/>
                        
                        We’ve reviewed a second report against your profile and are issuing a final warning.<br/>
                        <strong>Moderator’s note:</strong><br/>
                        <blockquote style=""margin:0 0 1em;padding-left:1em;border-left:3px solid #ccc;"">
                            {adminReason}
                        </blockquote>
                        
                        Continued concerns may lead to suspension of your account.<br/><br/>
                        
                        Important: We’ve also shared these reports with external networks (LinkedIn, Twitter, etc.) where your 
                        public profile appears—they’re reviewing these concerns as well.<br/><br/>
                        
                        If you’d like to discuss this decision or provide context, please reply to this email or reach us at 
                        <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.<br/><br/>
                        
                        Regards,<br/>
                        <em>The SkillSwap Support Team</em>";
                    }
                }
                else
                {
                    // Strike 3 → deactivate
                    entry.AdminAction = "RemoveUser";

                    // deactivate user
                    reported.IsActive = false;
                    reported.ModifiedDate = DateTime.UtcNow;
                    _ctx.TblUsers.Update(reported);

                    subject = "Account Deactivated: Multiple Reports Reviewed";
                    body = $@"
                        Hello {reported.FirstName},<br/><br/>

                        After reviewing a third report against your profile—and issuing two prior warnings—we have deactivated your SkillSwap account.<br/>
                        <strong>Moderator’s note:</strong><br/>
                        <blockquote style=""margin:0 0 1em;padding-left:1em;border-left:3px solid #ccc;"">
                            {adminReason}
                        </blockquote>

                        This action is final. We have also shared these reports with external networks  
                        (LinkedIn, Twitter, etc.) where your public profile appears; they are reviewing these concerns on their end as well.<br/><br/>

                        If you believe this decision is in error or wish to appeal, please reply to this email or contact us at 
                        <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a> within the next <strong>7 days</strong>.  
                        After that period, reactivation requests will no longer be considered.<br/><br/>

                        We regret that it has come to this, and we thank you for the time you spent with SkillSwap.<br/><br/>

                        Regards,<br/>
                        <em>The SkillSwap Support Team</em>";
                }

                // 4) Persist the new warning/removal entry
                await _repo.AddAsync(entry);
                await _ctx.SaveChangesAsync();

                // 5) Email the reported user
                await _emailService.SendEmailAsync(reported.Email, subject, body);

                // 6) Optionally thank the original reporter
                var reporter = await _userService.GetUserByIdAsync(original.FlaggedByUserId ?? 0);
                if (reporter != null)
                {
                    var repSubject = "Thank you for your report";
                    var repBody = $@"
                         Hi {reporter.FirstName},<br/><br/>
                         We’ve {(warnCount < 2 ? "issued a warning on" : "deactivated")} the account you reported (<strong>{reported.UserName}</strong>).<br/><br/>
                         — The SkillSwap Support Team";
                    await _emailService.SendEmailAsync(reporter.Email, repSubject, repBody);
                }
            }
        }

        public async Task<PagedResult<UserFlagHistoryVM>> GetAllFlagHistoriesAsync(int page, int pageSize)
        {
            // flatten history
            var all = await _ctx.TblUserFlags
                .Include(f => f.FlaggedByUser)
                .Include(f => f.AdminUser)
                .Include(f => f.FlaggedUser)
                .AsNoTracking()
                .ToListAsync();

            var grouped = all
            .GroupBy(f => new { f.FlaggedUserId, f.FlaggedUser.UserName })
            .Select(g => new UserFlagHistoryVM
            {
                UserId = g.Key.FlaggedUserId,
                UserName = g.Key.UserName,

                // 3) Build the per-user history list, newest-first
                History = g
                    .OrderByDescending(f => f.FlaggedDate)
                    .Select(f => new UserFlagHistoryItem
                    {
                        FlagId = f.UserFlagId,
                        FlaggedBy = f.FlaggedByUser?.UserName ?? "ADMIN ACTION",
                        FlaggedDate = f.FlaggedDate,

                        // action may be null until admin acts
                        ActionTaken = f.AdminAction,
                        AdminUser = f.AdminUser?.UserName,
                        ActionDate = f.AdminActionDate,
                        AdminReason = f.AdminReason
                    })
                    .ToList()
            })
            // 4) optional: sort your users alphabetically (or however you like)
            .OrderBy(vm => vm.UserName)
            .ToList();


            var total = grouped.Count();
            var pageItems = grouped
                .OrderBy(vm => vm.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<UserFlagHistoryVM>
            {
                Items = pageItems,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<FlaggedUserSummary>> GetFlaggedUserSummariesAsync(int page, int pageSize)
        {
            // 1) build the grouping + projection (but don’t execute yet)
            var q = _ctx.TblUserFlags
                 .AsNoTracking()
                 .Where(f => f.AdminAction == null)
                 .GroupBy(f => f.FlaggedUserId)
                 .Select(g => new FlaggedUserSummary
                 {
                     UserId = g.Key,
                     TotalFlags = g.Count()
                 })
                 .Join(
                     _ctx.TblUsers,
                     grp => grp.UserId,
                     u => u.UserId,
                     (grp, u) => new FlaggedUserSummary
                     {
                         UserId = u.UserId,
                         UserName = u.UserName,
                         FirstName = u.FirstName,
                         LastName = u.LastName,
                         Email = u.Email,
                         TotalFlags = grp.TotalFlags
                     }
                 );

            // 2) get total
            var total = await q.CountAsync();

            // 3) page & order
            var items = await q
                .OrderByDescending(x => x.TotalFlags)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<FlaggedUserSummary>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<bool> HasPendingFlagAsync(int flaggedUserId, int flaggedByUserId)
        {
            return await _ctx.TblUserFlags
                .AnyAsync(f =>
                   f.FlaggedUserId == flaggedUserId
                && f.FlaggedByUserId == flaggedByUserId
                && f.AdminAction == null
                );
        }

        public async Task<PagedResult<TblUserFlag>> GetFlagsForUserAsync(int flaggedUserId, int page, int pageSize)
        {
            var q = _ctx.TblUserFlags
                        .Where(f => f.FlaggedUserId == flaggedUserId)
                        .Include(f => f.FlaggedByUser)
                        .Include(f => f.AdminUser)
                        .AsNoTracking()
                        .OrderByDescending(f => f.FlaggedDate);

            var total = await q.CountAsync();
            var items = await q
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

            return new PagedResult<TblUserFlag>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

    }
}
