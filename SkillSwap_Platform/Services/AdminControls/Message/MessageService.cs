using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.Message
{
    public class MessageService : IMessageService
    {
        private readonly SkillSwapDbContext _ctx;
        private readonly ILogger<MessageService> _log;

        public MessageService(
            SkillSwapDbContext ctx,
            ILogger<MessageService> log)
        {
            _ctx = ctx;
            _log = log;
        }

        public async Task<PagedResult<HeldMessageVM>> GetHeldMessagesAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            var query = _ctx.TblMessages
                            .AsNoTracking()
                            .Where(m => m.IsFlagged && !m.IsApproved && m.ApprovedDate == null)
                            .Include(m => m.SenderUser)
                            .Include(m => m.ReceiverUser)
                            .OrderBy(m => m.SentDate);

            var total = await query.CountAsync();

            var pageItems = await query
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

            var vms = new List<HeldMessageVM>(pageItems.Count);
            foreach (var m in pageItems)
            {
                var words = await _ctx.UserSensitiveWords
                    .AsNoTracking()
                    .Where(usw => usw.MessageId == m.MessageId)
                    .Select(usw => usw.SensitiveWord.Word)
                    .ToListAsync();

                vms.Add(new HeldMessageVM
                {
                    MessageId = m.MessageId,
                    SenderName = m.SenderUser?.UserName ?? "Unknown",
                    ReceiverName = m.ReceiverUser?.UserName ?? "Unknown",
                    SentDate = m.SentDate,
                    Content = m.Content,
                    FlaggedWords = words
                });
            }

            return new PagedResult<HeldMessageVM>
            {
                Items = vms,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<FlaggedUserSummaryVM>> GetFlaggedUserSummariesAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;

            // 1) Find all senders who have at least one sensitive-word hit
            var flaggedSenders = _ctx.UserSensitiveWords
                .AsNoTracking()
                .GroupBy(usw => usw.Message.SenderUserId)
                .Select(g => new
                {
                    SenderUserId = g.Key,
                    TotalFlags = g.Count(),
                    LastFlaggedUtc = g.Max(usw => usw.Message.SentDate)
                });

            // 2) Join back to Users to get their name
            var query = from fs in flaggedSenders
                        join u in _ctx.TblUsers.AsNoTracking() on fs.SenderUserId equals u.UserId
                        orderby fs.TotalFlags descending
                        select new FlaggedUserSummaryVM
                        {
                            SenderUserId = u.UserId,
                            SenderUserName = u.UserName,
                            TotalFlags = fs.TotalFlags,
                            LastFlaggedDate = fs.LastFlaggedUtc
                        };

            // 3) Paging
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<FlaggedUserSummaryVM>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<HeldMessageVM>> GetHeldMessagesBySenderAsync(int senderUserId, int page, int pageSize)
        {
            if (page < 1) page = 1;

            var baseQuery = _ctx.TblMessages
                .AsNoTracking()
                .Where(m => m.SenderUserId == senderUserId
                         && _ctx.UserSensitiveWords
                                .AsNoTracking()
                                .Any(usw => usw.MessageId == m.MessageId))
                .Include(m => m.SenderUser)
                .Include(m => m.ReceiverUser)
                .Include(m => m.ApprovedByAdmin)
                .OrderBy(m => m.SentDate);

            var total = await baseQuery.CountAsync();
            var pageItems = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vms = new List<HeldMessageVM>(pageItems.Count);
            foreach (var m in pageItems)
            {
                // fetch each word + its warning text
                var details = await _ctx.UserSensitiveWords
                    .AsNoTracking()
                    .Where(usw => usw.MessageId == m.MessageId)
                    .Select(usw => new FlaggedWordDetail
                    {
                        Word = usw.SensitiveWord.Word,
                        WarningMessage = usw.SensitiveWord.WarningMessage
                    })
                    .ToListAsync();

                vms.Add(new HeldMessageVM
                {
                    MessageId = m.MessageId,
                    SenderName = m.SenderUser?.UserName ?? "Unknown",
                    ReceiverName = m.ReceiverUser?.UserName ?? "Unknown",
                    SentDate = m.SentDate,
                    Content = m.Content,
                    FlaggedWordDetails = details,
                    Status = m.IsFlagged
                                            ? "Held"
                                            : (m.IsApproved ? "Approved" : "Dismissed"),
                    AdminUser = m.ApprovedByAdmin?.UserName,
                    ActionDate = m.ApprovedDate
                });
            }

            return new PagedResult<HeldMessageVM>
            {
                Items = vms,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task ApproveMessageAsync(int messageId, int adminId)
        {
            try
            {
                var msg = await _ctx.TblMessages.FindAsync(messageId)
                          ?? throw new KeyNotFoundException($"Message {messageId} not found.");

                msg.IsApproved = true;
                msg.IsFlagged = false;

                // optionally track who approved:
                msg.ApprovedByAdminId = adminId;
                msg.ApprovedDate = DateTime.UtcNow;

                _ctx.TblMessages.Update(msg);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error approving message {MessageId}", messageId);
                throw;
            }
        }

        public async Task DismissMessageAsync(int messageId, int adminId)
        {
            using var tx = await _ctx.Database.BeginTransactionAsync();
            try
            {
                var msg = await _ctx.TblMessages.FindAsync(messageId)
                          ?? throw new KeyNotFoundException($"Message {messageId} not found.");

                // clear the flag, leave it “unapproved”, and replace the content
                msg.IsFlagged = true;
                msg.IsApproved = false;
                msg.ApprovedByAdminId = adminId;
                msg.ApprovedDate = DateTime.UtcNow;

                _ctx.TblMessages.Update(msg);
                await _ctx.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error dismissing message {MessageId}", messageId);
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}
