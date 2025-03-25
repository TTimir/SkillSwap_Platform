using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Repository;

namespace SkillSwap_Platform.Services
{
    public interface IMessagingService
    {
        Task<TblMessage?> GetMessageAsync(int messageId);
        Task<IEnumerable<TblMessage>> GetUserMessagesAsync(int userId);
        Task<IEnumerable<TblMessage>> GetConversationAsync(int userAId, int userBId);
        Task<int> SendMessageAsync(
            int senderId,
            int receiverId,
            string content,
            string? meetingLink,
            IEnumerable<(string fileName, string filePath, string contentType)>? attachments
        );
        Task MarkMessageAsReadAsync(int messageId);
    }
}