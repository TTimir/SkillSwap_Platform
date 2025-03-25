using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.MessagesVM;
using SkillSwap_Platform.Services;
using System.Diagnostics;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class MessagingController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<MessagingController> _logger;
        private readonly ISensitiveWordService _sensitiveWordService;

        public MessagingController(SkillSwapDbContext context,
                                         ILogger<MessagingController> logger,
                                         ISensitiveWordService sensitiveWordService)
        {
            _context = context;
            _logger = logger;
            _sensitiveWordService = sensitiveWordService;
        }


        #region Conversation & Chat Contacts
        /// <summary>
        /// Displays the conversation between the logged-in user and the specified other user.
        /// </summary>
        /// <param name="otherUserId">The ID of the other participant.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Conversation(int? otherUserId)
        {
            try
            {
                int currentUserId = GetUserId();

                // 1. Get all distinct conversation partner IDs for the current user
                var partnerIds = await _context.TblMessages
                    .Where(m => m.SenderUserId == currentUserId || m.ReceiverUserId == currentUserId)
                    .Select(m => m.SenderUserId == currentUserId ? m.ReceiverUserId : m.SenderUserId)
                    .Distinct()
                    .ToListAsync();

                // 2. Build a list of chat members (all users you’ve messaged with)
                var chatMembers = new List<ChatMemberVM>();
                foreach (var partnerId in partnerIds)
                {
                    var user = await _context.TblUsers.FindAsync(partnerId);
                    if (user != null)
                    {
                        // Retrieve all messages between the current user and this partner
                        var conversationMessages = await _context.TblMessages
                            .Where(m =>
                                (m.SenderUserId == currentUserId && m.ReceiverUserId == partnerId) ||
                                (m.SenderUserId == partnerId && m.ReceiverUserId == currentUserId))
                            .OrderBy(m => m.SentDate)
                            .ToListAsync();

                        chatMembers.Add(new ChatMemberVM
                        {
                            UserID = partnerId,
                            UserName = user.UserName,
                            ProfileImage = user.ProfileImageUrl,
                            Designation = user.Designation,
                            LastMessageTime = conversationMessages.Any()
                                                ? conversationMessages.Last().SentDate.ToLocalTime().ToString("g")
                                                : "",
                            UnreadCount = conversationMessages.Count(m =>
                                m.ReceiverUserId == currentUserId &&
                                !m.IsRead &&
                                (!m.IsFlagged || (m.IsFlagged && m.IsApproved))
                            )
                        });
                    }
                }

                // Optional: sort the chat members by last message time (most recent first)
                chatMembers = chatMembers.OrderByDescending(cm =>
                    DateTime.TryParse(cm.LastMessageTime, out DateTime dt) ? dt : DateTime.MinValue)
                    .ToList();

                // 3. If no specific conversation is selected, set otherUserId to 0.
                if (!otherUserId.HasValue)
                {
                    otherUserId = 0;
                }

                // 4. Load messages for the selected conversation (if any)
                List<TblMessage> messages = new List<TblMessage>();
                TblUser otherUser = null;
                bool otherUserOnline = false;

                if (otherUserId.HasValue && otherUserId.Value > 0)
                {
                    // Validate: Check that the other user exists.
                    otherUser = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == otherUserId.Value);
                    if (otherUser == null)
                    {
                        string errorMsg = "The user you are trying to chat with does not exist.";
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            TempData["ErrorMessage"] = errorMsg;
                            return Json(new { success = false, error = errorMsg });
                        }
                    }

                    // When the current user is the receiver, filter out messages that are flagged and not approved.
                    if (otherUserId.Value != currentUserId)
                    {
                        messages = await _context.TblMessages
                            .Include(m => m.SenderUser)
                            .Include(m => m.TblMessageAttachments)
                            .Where(m =>
                                ((m.SenderUserId == currentUserId && m.ReceiverUserId == otherUserId.Value) ||
                                 (m.SenderUserId == otherUserId.Value && m.ReceiverUserId == currentUserId))
                                 &&
                                (
                                    // Always show messages where current user is sender.
                                    (m.SenderUserId == currentUserId)
                                    ||
                                    // For receiver, only show if not flagged or approved.
                                    (!m.IsFlagged || (m.IsFlagged && m.IsApproved))
                                )
                            )
                            .OrderBy(m => m.SentDate)
                            .ToListAsync();
                    }
                    else
                    {
                        // For the sender, load all messages.
                        messages = await _context.TblMessages
                            .Include(m => m.SenderUser)
                            .Include(m => m.TblMessageAttachments)
                            .Where(m =>
                                (m.SenderUserId == currentUserId && m.ReceiverUserId == otherUserId.Value) ||
                                (m.SenderUserId == otherUserId.Value && m.ReceiverUserId == currentUserId))
                            .OrderBy(m => m.SentDate)
                            .ToListAsync();
                    }
                    // Mark messages as read for the current user.
                    var unreadMessages = messages
                        .Where(m => m.ReceiverUserId == currentUserId && !m.IsRead)
                        .ToList();

                    if (unreadMessages.Any())
                    {
                        foreach (var msg in unreadMessages)
                        {
                            msg.IsRead = true;
                        }
                        await _context.SaveChangesAsync();
                    }

                    otherUser = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == otherUserId.Value);
                    if (otherUser != null && otherUser.LastActive.HasValue)
                    {
                        otherUserOnline = (DateTime.UtcNow - otherUser.LastActive.Value).TotalMinutes < 10;
                    }
                }

                // Load sensitive words from the database.
                var sensitiveWords = await _context.PrivacySensitiveWords
                    .Select(psw => psw.Word)
                    .ToListAsync();

                ViewBag.SensitiveWords = sensitiveWords;

                var viewModel = new ConversationVM
                {
                    CurrentUserID = currentUserId,
                    OtherUserId = otherUserId.HasValue ? otherUserId.Value : 0,
                    OtherUserName = otherUser?.UserName ?? "Unknown",
                    OtherUserProfileImage = otherUser?.ProfileImageUrl,
                    OtherUserIsOnline = otherUserOnline,
                    ChatMembers = chatMembers,
                    Messages = messages
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching conversation between user {CurrentUserId} and {OtherUserId}", GetUserId(), otherUserId);
                TempData["ErrorMessage"] = "An error occurred while loading your conversation.";
                return RedirectToAction("EP500", "EP");
            }
        }

        #endregion

        #region Send Message Action
        /// <summary>
        /// Sends a message from the current user to the specified receiver, optionally including attachments.
        /// </summary>
        /// <param name="receiverUserId">Receiver’s user ID</param>
        /// <param name="content">The message text</param>
        /// <param name="attachments">Any files attached to the message</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int receiverUserId, string content, IFormFileCollection attachments, string replyPreview, int? replyMessageId)
        {
            // Begin transaction for atomicity.
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    int senderUserId = GetUserId();

                    Debug.WriteLine($"MessagingController.SendMessage: SenderUserId = {senderUserId}");
                    // Normalize content: if null or whitespace, set to an empty string.
                    content = content?.Trim() ?? string.Empty;

                    // Optionally, you can enforce that either content or attachments must be provided.
                    if (string.IsNullOrEmpty(content) && (attachments == null || !attachments.Any()))
                    {
                        string errorMsg = "Please enter a message or attach a file.";
                        // If the request is AJAX, return a JSON error result.
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            TempData["ErrorMessage"] = errorMsg;
                            return Json(new { success = false, error = errorMsg });
                        }
                        TempData["ErrorMessage"] = errorMsg;
                        return RedirectToAction("Conversation", new { otherUserId = receiverUserId });
                    }


                    // Optional: Validate maximum length of content.
                    if (content.Length > 1000) // Example: maximum 1000 characters.
                    {
                        string errorMsg = "Message is too long. Please limit to 1000 characters.";
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            TempData["ErrorMessage"] = errorMsg;
                            return Json(new { success = false, error = errorMsg });
                        }
                        TempData["ErrorMessage"] = errorMsg;
                        return RedirectToAction("Conversation", new { otherUserId = receiverUserId });
                    }

                    // Check for sensitive words in the content.
                    var sensitiveWarnings = await _sensitiveWordService.CheckSensitiveWordsAsync(content);
                    bool isFlagged = sensitiveWarnings.Any();

                    // Create the new message.
                    var message = new TblMessage
                    {
                        SenderUserId = senderUserId,
                        ReceiverUserId = receiverUserId,
                        Content = content,
                        ReplyPreview = replyPreview,
                        ReplyToMessageId = (replyMessageId.HasValue && replyMessageId.Value > 0) ? replyMessageId : null,
                        SentDate = DateTime.UtcNow,
                        IsRead = false,
                        IsFlagged = isFlagged
                    };

                    _context.TblMessages.Add(message);
                    await _context.SaveChangesAsync();

                    // Process uploaded attachments.
                    if (attachments != null && attachments.Any())
                    {
                        // Allowed MIME types.
                        var allowedTypes = new[]
                        {
                            "image/jpeg", "image/png", "application/pdf",
                            "text/plain", "text/x-python", "text/x-c",
                            "application/javascript", "text/javascript",
                            "text/x-java-source", "text/x-c++src",
                            "application/zip", "application/x-zip-compressed"
                        };

                        // Validation: Check file size (e.g., max 5MB) and allowed types.
                        const long maxFileSize = 5 * 1024 * 1024; // 5MB

                        foreach (var file in attachments)
                        {
                            if (file != null && file.Length > 0)
                            {
                                if (file.Length > maxFileSize)
                                {
                                    string errorMsg = "One or more files exceed the maximum allowed size (5MB).";
                                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                    {
                                        TempData["ErrorMessage"] = errorMsg;
                                        return Json(new { success = false, error = errorMsg });
                                    }
                                    TempData["ErrorMessage"] = errorMsg;
                                    return RedirectToAction("Conversation", new { otherUserId = receiverUserId });
                                }

                                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                                {
                                    string errorMsg = "File type not allowed.";
                                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                    {
                                        TempData["ErrorMessage"] = errorMsg;
                                        return Json(new { success = false, error = errorMsg });
                                    }
                                    TempData["ErrorMessage"] = errorMsg;
                                    return RedirectToAction("Conversation", new { otherUserId = receiverUserId });
                                }

                                // add file validation such as size/type checking here.)
                                string filePath = await UploadFileAsync(file, "messageAttachments");

                                var attachment = new TblMessageAttachment
                                {
                                    MessageId = message.MessageId,
                                    FileName = file.FileName,
                                    FilePath = filePath,
                                    ContentType = file.ContentType,
                                    UploadedDate = DateTime.UtcNow
                                };

                                _context.TblMessageAttachments.Add(attachment);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // If the message is flagged, insert audit records for each sensitive word.
                    if (isFlagged)
                    {
                        foreach (var kvp in sensitiveWarnings)
                        {
                            // Get the SensitiveWord record using case-insensitive comparison.
                            var sensitiveRecord = await _context.SensitiveWords
                                .FirstOrDefaultAsync(sw => sw.Word.ToLower() == kvp.Key.ToLower());
                            if (sensitiveRecord != null)
                            {
                                _context.UserSensitiveWords.Add(new UserSensitiveWord
                                {
                                    UserId = senderUserId,
                                    MessageId = message.MessageId,
                                    SensitiveWordId = sensitiveRecord.Id,
                                    DetectedOn = DateTime.UtcNow
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Commit the transaction after successful operations.
                    await transaction.CommitAsync();

                    // If the request is AJAX, return a partial view for the new message.
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        var currentUser = await _context.TblUsers.FindAsync(senderUserId);
                        // Reload attachments from DB.
                        var attachmentsList = await _context.TblMessageAttachments
                            .Where(a => a.MessageId == message.MessageId)
                            .ToListAsync();

                        var messageVm = new MessageItemVM
                        {
                            MessageId = message.MessageId,
                            CurrentUserID = senderUserId,
                            SenderUserID = senderUserId,
                            SenderName = "You",
                            SenderProfileImage = currentUser?.ProfileImageUrl,
                            SentDate = message.SentDate,
                            Content = message.Content,
                            ReplyPreview = message.ReplyPreview,
                            ReplyMessageId = message.ReplyToMessageId,
                            IsRead = false,
                            ShowHeader = true,
                            Attachments = attachmentsList,
                            IsFlagged = message.IsFlagged, 
                            IsApproved = message.IsApproved
                        };

                        return PartialView("_MessageItem", messageVm);
                    }
                    else
                    {
                        // Fallback for non-AJAX requests.
                        return RedirectToAction("Conversation", new { otherUserId = receiverUserId });
                    }
                }
                catch (Exception ex)
                {
                    // Rollback on error.
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error sending message from user {SenderId} to user {ReceiverId}", GetUserId(), receiverUserId);
                    TempData["ErrorMessage"] = "An error occurred while sending your message.";
                    return RedirectToAction("EP500", "EP");
                }
            }
        }
        #endregion

        #region Attachment Retrieval
        /// <summary>
        /// Returns the attachments for a given message as a partial view.
        /// </summary>
        /// <param name="messageId">The ID of the message</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAttachments(int messageId)
        {
            try
            {
                var attachments = await _context.TblMessageAttachments
                    .Where(a => a.MessageId == messageId)
                    .ToListAsync();

                return PartialView("_MessageAttachments", attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching attachments for message {MessageID}", messageId);
                return RedirectToAction("EP500", "EP");
            }
        }
        #endregion

        #region Helper Class
        /// <summary>
        /// Helper to get the current logged in user's ID from claims.
        /// </summary>
        /// <returns></returns>
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            throw new Exception("User ID not found in claims.");
        }

        /// <summary>
        /// Uploads a file to the specified folder under wwwroot/uploads.
        /// </summary>
        /// <param name="file">The file to upload</param>
        /// <param name="folderName">The target folder name</param>
        /// <returns>The relative URL of the uploaded file</returns>
        private async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folderName);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return $"/uploads/{folderName}/{uniqueFileName}";
        }
        #endregion
    }
}