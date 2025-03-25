using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Repository;

namespace SkillSwap_Platform.Services
{
    public class MessagingService : IMessagingService
    {
        private readonly IMessageRepository _messageRepository;

        public MessagingService(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }

        public async Task<TblMessage?> GetMessageAsync(int messageId)
        {
            return await _messageRepository.GetMessageByIdAsync(messageId);
        }

        public async Task<IEnumerable<TblMessage>> GetUserMessagesAsync(int userId)
        {
            return await _messageRepository.GetUserMessagesAsync(userId);
        }

        public async Task<IEnumerable<TblMessage>> GetConversationAsync(int userAId, int userBId)
        {
            return await _messageRepository.GetConversationAsync(userAId, userBId);
        }

        public async Task<int> SendMessageAsync(
            int senderId,
            int receiverId,
            string content,
            string? meetingLink,
            IEnumerable<(string fileName, string filePath, string contentType)>? attachments
        )
        {
            // Basic example validations (extend as needed)
            if (senderId <= 0 || receiverId <= 0)
                throw new ArgumentException("Invalid sender or receiver ID.");

            // Construct the message entity
            var messageEntity = new TblMessage
            {
                SenderUserId = senderId,
                ReceiverUserId = receiverId,
                Content = content,
                MeetingLink = meetingLink,
                SentDate = DateTime.UtcNow,
                IsRead = false
            };

            // Construct the attachment entities if any
            List<TblMessageAttachment>? attachmentEntities = null;
            if (attachments != null)
            {
                attachmentEntities = new List<TblMessageAttachment>();
                foreach (var (fileName, filePath, contentType) in attachments)
                {
                    attachmentEntities.Add(new TblMessageAttachment
                    {
                        FileName = fileName,
                        FilePath = filePath,
                        ContentType = contentType,
                        // MessageID will be assigned after the message is saved
                    });
                }
            }

            // Insert in DB (with transaction) via repository
            await _messageRepository.CreateMessageAsync(messageEntity, attachmentEntities);

            // Return the newly created Message ID
            return messageEntity.MessageId;
        }

        public async Task MarkMessageAsReadAsync(int messageId)
        {
            await _messageRepository.MarkMessageAsReadAsync(messageId);
        }
    }
}
