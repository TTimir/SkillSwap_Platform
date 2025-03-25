using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Repository
{
    public interface IMessageRepository
    {
        Task<TblMessage?> GetMessageByIdAsync(int messageId);
        Task<IEnumerable<TblMessage>> GetUserMessagesAsync(int userId);
        Task<IEnumerable<TblMessage>> GetConversationAsync(int userAId, int userBId);
        Task CreateMessageAsync(TblMessage message, IEnumerable<TblMessageAttachment>? attachments);
        Task MarkMessageAsReadAsync(int messageId);
    }
}
