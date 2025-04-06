using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Repository
{
    public class MessageRepository : IMessageRepository
    {
        private readonly SkillSwapDbContext _dbContext;

        public MessageRepository(SkillSwapDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<TblMessage?> GetMessageByIdAsync(int messageId)
        {
            return await _dbContext.TblMessages
                .Include(m => m.TblMessageAttachments)
                .FirstOrDefaultAsync(m => m.MessageId == messageId);
        }

        public async Task<IEnumerable<TblMessage>> GetUserMessagesAsync(int userId)
        {
            return await _dbContext.TblMessages
                .Include(m => m.TblMessageAttachments)
                .Where(m => m.SenderUserId == userId || m.ReceiverUserId == userId)
                .OrderByDescending(m => m.SentDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<TblMessage>> GetConversationAsync(int userAId, int userBId)
        {
            return await _dbContext.TblMessages
                .Include(m => m.TblMessageAttachments)
                .Where(m =>
                    m.SenderUserId == userAId && m.ReceiverUserId == userBId ||
                    m.SenderUserId == userBId && m.ReceiverUserId == userAId
                )
                .OrderBy(m => m.SentDate)
                .ToListAsync();
        }

        public async Task CreateMessageAsync(TblMessage message, IEnumerable<TblMessageAttachment>? attachments)
        {
            // BEGIN TRANSACTION
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Insert the message
                await _dbContext.TblMessages.AddAsync(message);
                await _dbContext.SaveChangesAsync();

                // Insert attachments if any
                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        attachment.MessageId = message.MessageId;
                        attachment.UploadedDate = DateTime.UtcNow;
                    }
                    await _dbContext.TblMessageAttachments.AddRangeAsync(attachments);
                    await _dbContext.SaveChangesAsync();
                }

                // COMMIT TRANSACTION
                await transaction.CommitAsync();
            }
            catch
            {
                // ROLLBACK TRANSACTION
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task MarkMessageAsReadAsync(int messageId)
        {
            var message = await _dbContext.TblMessages.FindAsync(messageId);
            if (message != null)
            {
                message.IsRead = true;
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
