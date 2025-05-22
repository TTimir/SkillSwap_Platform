using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.MessagesVM;
using SkillSwap_Platform.Services;
using SkillSwap_Platform.Services.Email;
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
        private readonly IEmailService _emailService;
        public MessagingController(SkillSwapDbContext context,
                                         ILogger<MessagingController> logger,
                                         ISensitiveWordService sensitiveWordService,
                                         IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _sensitiveWordService = sensitiveWordService;
            _emailService = emailService;
        }


        #region Conversation & Chat Contacts
        /// <summary>
        /// Displays the conversation between the logged-in user and the specified other user.
        /// </summary>
        /// <param name="otherUserId">The ID of the other participant.</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Conversation(int? otherUserId, int? offerId, string searchTerm)
        {
            try
            {
                int currentUserId = GetUserId();


                if (offerId.HasValue)
                {
                    // **Important Change: Include the User navigation property**
                    var tblOffer = await _context.TblOffers
                        .Include(o => o.User) // Load the offer owner
                        .FirstOrDefaultAsync(o => o.OfferId == offerId.Value);

                    if (tblOffer != null)
                    {
                        int offerOwnerId = tblOffer.User != null ? tblOffer.User.UserId : tblOffer.UserId;

                        // Map properties from tblOffer to OfferDisplayVM
                        var offerDisplay = new OfferDisplayVM
                        {
                            OfferId = tblOffer.OfferId,
                            Title = tblOffer.Title,
                            ShortTitle = tblOffer.Title,
                            TimeCommitmentDays = tblOffer.TimeCommitmentDays,
                            Category = tblOffer.Category,
                            PortfolioUrls = !string.IsNullOrWhiteSpace(tblOffer.Portfolio)
                                                ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(tblOffer.Portfolio)
                                                : new List<string>(),
                            SkillNames = !string.IsNullOrWhiteSpace(tblOffer.SkillIdOfferOwner)
                                ? await GetOfferedSkillNames(tblOffer.SkillIdOfferOwner)
                                : new List<string>(),
                            OfferOwnerId = offerOwnerId,
                            WillingSkills = tblOffer.WillingSkill?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>(),
                        };

                        // Pass the mapped offer details to the view.
                        ViewBag.OfferDetails = offerDisplay;

                        // Set the conversation partner to the offer owner.
                        otherUserId = tblOffer.UserId;
                    }
                }

                // 1. Get all distinct conversation partner IDs for the current user
                var partnerIds = await _context.TblMessages
                    .Where(m => !m.IsDeleted
                             && (m.SenderUserId == currentUserId
                              || m.ReceiverUserId == currentUserId))
                    .Select(m => m.SenderUserId == currentUserId
                                  ? m.ReceiverUserId
                                  : m.SenderUserId)
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

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.Trim();
                    chatMembers = chatMembers
                        .Where(c =>
                            c.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)
                         || c.Designation.Contains(term, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // sort the chat members by last message time (most recent first)
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
                            .Include(m => m.ApprovedByAdmin)
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

                // --- Prepare message view models.
                var messageViewModels = new List<MessageItemVM>();

                // Remove the offerDisplayedInChat flag so that every offer message loads its offer preview.
                foreach (var msg in messages)
                {
                    int? effectiveOfferId = msg.OfferId ?? offerId;

                    if (msg.OfferId.HasValue)
                    {
                        // Load the offer details for every message that has an OfferId.
                        var tblOffer = await _context.TblOffers
                            .Include(o => o.User)
                            .FirstOrDefaultAsync(o => o.OfferId == effectiveOfferId.Value);
                        if (tblOffer != null)
                        {
                            List<string> offeredSkillNames = !string.IsNullOrWhiteSpace(tblOffer.SkillIdOfferOwner)
                                ? await GetOfferedSkillNames(tblOffer.SkillIdOfferOwner)
                                : new List<string>();

                            msg.OfferPreview = new OfferDisplayVM
                            {
                                OfferId = tblOffer.OfferId,
                                Title = tblOffer.Title,
                                ShortTitle = tblOffer.Title,
                                TimeCommitmentDays = tblOffer.TimeCommitmentDays,
                                Category = tblOffer.Category,
                                PortfolioUrls = !string.IsNullOrWhiteSpace(tblOffer.Portfolio)
                                                ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(tblOffer.Portfolio)
                                                : new List<string>(),
                                SkillNames = offeredSkillNames,
                                OfferOwnerId = tblOffer.User != null ? tblOffer.User.UserId : tblOffer.UserId,
                                WillingSkills = tblOffer.WillingSkill?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>()
                            };
                        }
                        else
                        {
                            // In case the offer is missing from the database.
                            msg.OfferPreview = null;
                        }
                    }
                    else
                    {
                        msg.OfferPreview = null;
                    }

                    var contract = await _context.TblContracts
                            .Where(c => c.MessageId == msg.MessageId)
                            .OrderByDescending(c => c.Version)
                            .FirstOrDefaultAsync();

                    string? offeredSkillName = null;
                    if (!string.IsNullOrWhiteSpace(contract?.OfferedSkill) && int.TryParse(contract.OfferedSkill, out int offeredSkillId))
                    {
                        offeredSkillName = await _context.TblSkills
                            .Where(s => s.SkillId == offeredSkillId)
                            .Select(s => s.SkillName)
                            .FirstOrDefaultAsync();
                    }
                    else
                    {
                        // fallback: treat as raw skill name
                        offeredSkillName = contract?.OfferedSkill;
                    }

                    // Resolve ExchangeSkill
                    string? exchangeSkillName = null;
                    if (!string.IsNullOrWhiteSpace(contract?.ReceiverSkill) && int.TryParse(contract.ReceiverSkill, out int exchangeSkillId))
                    {
                        exchangeSkillName = await _context.TblSkills
                            .Where(s => s.SkillId == exchangeSkillId)
                            .Select(s => s.SkillName)
                            .FirstOrDefaultAsync();
                    }
                    else
                    {
                        exchangeSkillName = contract?.ReceiverSkill;
                    }

                    int exchangeId = 0;
                    OfferDisplayVM? offerDisplay = null;
                    if (msg.MessageType == "ResourceNotification" && msg.ResourceId.HasValue)
                    {
                        // Retrieve the resource record using the resource identifier.
                        var resource = await _context.TblResources.FirstOrDefaultAsync(r => r.ResourceId == msg.ResourceId);
                        if (resource != null)
                        {
                            // Assuming your TblResource entity contains these properties.
                            exchangeId = resource.ExchangeId ?? 0;
                            offerId = resource.OfferId;

                            // Optionally populate additional details.
                            offerDisplay = new OfferDisplayVM
                            {
                                OfferId = resource.OfferId ?? 0,
                                Title = resource.Title,
                            };
                        }
                    }

                    TblExchange? exchangeEntity = null;
                    if (msg.MessageType == "InPersonMeetNotification" && msg.ExchangeId != null)
                    {
                        exchangeEntity = await _context.TblExchanges
                                           .FirstOrDefaultAsync(e => e.ExchangeId == msg.ExchangeId);
                    }
                    // If this message represents an in-person meeting notification,
                    // load the matching meeting record.
                    TblInPersonMeeting meeting = null;
                    if (msg.MessageType == "InPersonMeetNotification")
                    {
                        meeting = await _context.TblInPersonMeetings
                            .Where(m => m.ExchangeId == msg.ExchangeId)
                            .OrderByDescending(m => m.CreatedDate)
                            .FirstOrDefaultAsync();
                    }

                    // Lookup the usernames based on the exchange record, if available
                    string exchangeOfferOwnerName = "N/A";
                    string exchangeOtherUserName = "N/A";
                    if (exchangeEntity != null)
                    {
                        var offerOwnerUser = await _context.TblUsers.FindAsync(exchangeEntity.OfferOwnerId);
                        var otherUserEntity = await _context.TblUsers.FindAsync(exchangeEntity.OtherUserId);
                        if (offerOwnerUser != null)
                            exchangeOfferOwnerName = offerOwnerUser.UserName;
                        if (otherUserEntity != null)
                            exchangeOtherUserName = otherUserEntity.UserName;
                    }

                    messageViewModels.Add(new MessageItemVM
                    {
                        MessageId = msg.MessageId,
                        CurrentUserID = currentUserId,
                        SenderUserID = msg.SenderUserId,
                        SenderName = msg.SenderUser?.UserName ?? "Unknown",
                        SenderProfileImage = msg.SenderUser?.ProfileImageUrl,
                        SentDate = msg.SentDate,
                        Content = msg.Content,
                        MessageType = msg.MessageType,
                        ResourceId = msg.ResourceId,
                        ReplyPreview = msg.ReplyPreview,
                        ReplyMessageId = msg.ReplyToMessageId,
                        Attachments = msg.TblMessageAttachments,
                        IsRead = msg.IsRead,
                        IsFlagged = msg.IsFlagged,
                        IsApproved = msg.IsApproved,
                        OfferDetails = msg.OfferPreview,
                        ExchangeId = exchangeId,
                        OfferId = offerId,
                        ContractDetails = contract,
                        OfferedSkillName = offeredSkillName,
                        ReceiverSkillName = exchangeSkillName,
                        Exchange = exchangeEntity,
                        InPersonMeeting = meeting,
                        ExchangeOfferOwnerName = exchangeOfferOwnerName,
                        ExchangeOtherUserName = exchangeOtherUserName,
                        ApprovedByAdminId = msg.ApprovedByAdminId,
                        ApprovedDate = msg.ApprovedDate,
                        ApprovedByAdminName = msg.ApprovedByAdmin?.UserName
                    });
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
                    Messages = messageViewModels,
                    OfferId = offerId,
                    SearchTerm = searchTerm
                };

                // 1️⃣ Fetch all contract records between the two users, ordered newest first:
                var allContracts = await _context.TblContracts
                    .Where(c =>
                        (c.SenderUserId == currentUserId && c.ReceiverUserId == otherUserId) ||
                        (c.SenderUserId == otherUserId && c.ReceiverUserId == currentUserId))
                    .Include(c => c.Offer)
                    .OrderByDescending(c => c.CreatedDate)
                    .ToListAsync();

                // 2️⃣ Count distinct offers:
                viewModel.TotalSwapOffersCount = allContracts
                    .Select(c => c.OfferId)
                    .Distinct()
                    .Count();

                // 3️⃣ For each offer, pick the very first record (latest date):
                viewModel.SwapOffers = allContracts
                    .GroupBy(c => c.OfferId)
                    .Select(g => {
                        var newest = g.First();  // because we ordered Descending
                        return new OfferInviteVM
                        {
                            OfferId = g.Key,
                            Title = newest.Offer.Title,
                            LatestStatus = newest.Status,
                            LatestDate = newest.CreatedDate
                        };
                    })
                    .OrderByDescending(o => o.LatestDate)
                    .Take(3)
                    .ToList();

                // After viewModel is mostly built...
                var latestContract = await _context.TblContracts
                    .Include(c => c.Offer)
                    .Where(c =>
                        (c.SenderUserId == currentUserId && c.ReceiverUserId == otherUserId) ||
                        (c.SenderUserId == otherUserId && c.ReceiverUserId == currentUserId))
                    .Where(c => c.Status != "Declined")           // only “active” ones
                    .OrderByDescending(c => c.UpdatedDate)        // or CreatedDate if you prefer
                    .FirstOrDefaultAsync();

                if (latestContract != null)
                {
                    viewModel.LatestActiveOffer = new LatestOfferVM
                    {
                        OfferId = latestContract.OfferId,
                        Title = latestContract.Offer.Title,
                        ContractUniqueId = latestContract.ContractUniqueId,
                        CurrentStagePdfUrl = latestContract.ContractDocument,
                        // assume you have a PDF URL stored in the contract record
                        Status = latestContract.Status
                    };
                }
                else
                {
                    viewModel.LatestActiveOffer = null;
                }

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

                    // Normalize content: if null or whitespace, set to an empty string.
                    content = content?.Trim() ?? string.Empty;

                    // Check for sensitive words in the content.
                    var sensitiveWarnings = await _sensitiveWordService.CheckSensitiveWordsAsync(content);
                    bool isFlagged = sensitiveWarnings.Any();

                    //if (isFlagged)
                    //{
                    //    TempData["ErrorMessage"] = "Your message has been held for review due to inappropriate content.";
                    //    return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    //        ? Json(new { success = false, error = TempData["ErrorMessage"] })
                    //        : RedirectToAction("Conversation", new { otherUserId = receiverUserId });
                    //}

                    // pull it only from the form…
                    int? usedOfferId = null;
                    if (Request.Form.ContainsKey("offerId")
                        && int.TryParse(Request.Form["offerId"], out var oid))
                    {
                        usedOfferId = oid;
                    }


                    // enforce that either content or attachments must be provided.
                    if (string.IsNullOrEmpty(content) && (attachments == null || !attachments.Any()) && !usedOfferId.HasValue)
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


                    // Validate maximum length of content.
                    if (content.Length > 200) // Example: maximum 200 characters.
                    {
                        string errorMsg = "Message is too long. Please limit to 200 characters.";
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            TempData["ErrorMessage"] = errorMsg;
                            return Json(new { success = false, error = errorMsg });
                        }
                        TempData["ErrorMessage"] = errorMsg;
                        return Json(new { success = false, error = errorMsg });
                    }

                    // only enforce “no duplicate” when it’s a true offer‐only invite
                    bool isOfferInvite = usedOfferId.HasValue
                                         && string.IsNullOrWhiteSpace(content)
                                         && (attachments == null || !attachments.Any());

                    string messageType = isOfferInvite
                        ? "OfferInvite"
                        : string.IsNullOrEmpty(content) && attachments.Any()
                            ? (attachments.Count == 1 && attachments.First().ContentType.StartsWith("image/")
                                ? "Image"
                                : "File")
                            : "Normal";

                    // ONLY for real offer invites do we enforce “no duplicate contract”
                    if (isOfferInvite)
                    {
                        // ⬇︎ NEW: block duplicate invite messages
                        bool alreadyInvited = await _context.TblMessages.AnyAsync(m =>
                            m.SenderUserId == senderUserId &&
                            m.ReceiverUserId == receiverUserId &&
                            m.OfferId == usedOfferId.Value);

                        if (alreadyInvited)
                        {
                            string errorMsg = "You have already invited this user to that offer.";
                            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                return Json(new { success = false, error = errorMsg });
                            TempData["ErrorMessage"] = errorMsg;
                            return RedirectToAction("Conversation", new { otherUserId = receiverUserId });
                        }

                        var existingContract = await _context.TblContracts
                            .Where(c =>
                                c.OfferId == usedOfferId.Value &&
                                ((c.SenderUserId == senderUserId && c.ReceiverUserId == receiverUserId) ||
                                 (c.SenderUserId == receiverUserId && c.ReceiverUserId == senderUserId))
                            )
                            .OrderByDescending(c => c.DeclinedDate ?? c.CreatedDate)
                            .FirstOrDefaultAsync();

                        if (existingContract != null)
                        {
                            if (existingContract.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
                            {
                                string errorMsg = "A contract for this conversation has already been accepted.";
                                return Json(new { success = false, error = errorMsg });
                            }

                            if (existingContract.Status.Equals("Declined", StringComparison.OrdinalIgnoreCase))
                            {
                                DateTime referenceDate = existingContract.DeclinedDate ?? existingContract.CreatedDate;
                                if ((DateTime.UtcNow - referenceDate).TotalDays < 5)
                                {
                                    string errorMsg = "You can resend the contract invite only 5 days after the previous invite was declined.";
                                    return Json(new { success = false, error = errorMsg });
                                }
                            }

                            if (existingContract.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                                || existingContract.Status.Equals("Review", StringComparison.OrdinalIgnoreCase))
                            {
                                string errorMsg = "A contract for this conversation is already pending review.";
                                return Json(new { success = false, error = errorMsg });
                            }
                        }
                    }

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
                        IsFlagged = isFlagged,
                        IsApproved = !isFlagged,
                        OfferId = usedOfferId,
                        MessageType = messageType
                    };

                    _context.TblMessages.Add(message);
                    await _context.SaveChangesAsync();

                    // → AFTER you persist the message, fire the email
                    var receiver = await _context.TblUsers
                                       .Where(u => u.UserId == receiverUserId)
                                       .Select(u => new { u.Email, u.UserName })
                                       .SingleAsync();

                    var conversationUrl = Url.Action(
                        "Conversation",
                        "Messaging",
                        new { otherUserId = GetUserId() },
                        Request.Scheme,
                        Request.Host.ToString()
                    );

                    var htmlBody = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">

      <!-- Header -->
      <tr>
        <td style=""background:#00A88F;color:#ffffff;padding:15px;text-align:center;font-size:20px;font-weight:bold;"">
          Swapo
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.5;"">
          <p style=""margin:0 0 15px;"">Hi <strong>{receiver.UserName}</strong>,</p>
          <p style=""margin:0 0 15px;"">
            You have a new message from <strong>{User.Identity.Name}</strong>:
          </p>
          <blockquote style=""margin:0 0 20px;padding-left:1em;border-left:3px solid #ccc;color:#555;"">
            {System.Net.WebUtility.HtmlEncode(content)}
          </blockquote>
          <p style=""margin:0 0 20px;"">
            <a href=""{conversationUrl}"" style=""color:#00A88F;text-decoration:underline;"">
              View it on Swapo
            </a>
          </p>
        </td>
      </tr>

      <!-- Footer -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Have questions? <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">Contact Support</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>
";

                    // 3) send it
                    await _emailService.SendEmailAsync(
                        to: receiver.Email,
                        subject: $"New message from {User.Identity.Name}",
                        body: htmlBody,
                        isBodyHtml: true
                    );

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

                    // 4) Build offerDisplay only for a real invite
                    OfferDisplayVM? offerDisplay = null;
                    if (isOfferInvite)
                    {
                        var tblOffer = await _context.TblOffers
                            .Include(o => o.User)
                            .FirstOrDefaultAsync(o => o.OfferId == usedOfferId.Value);
                        if (tblOffer != null)
                        {
                            int offerOwnerId = (tblOffer.User != null) ? tblOffer.User.UserId : tblOffer.UserId;

                            offerDisplay = new OfferDisplayVM
                            {
                                OfferId = tblOffer.OfferId,
                                Title = tblOffer.Title,
                                TokenCost = (int)tblOffer.TokenCost,
                                ShortTitle = tblOffer.Title,
                                TimeCommitmentDays = tblOffer.TimeCommitmentDays,
                                Category = tblOffer.Category,
                                PortfolioUrls = !string.IsNullOrWhiteSpace(tblOffer.Portfolio)
                                                ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(tblOffer.Portfolio)
                                                : new List<string>(),
                                SkillNames = !string.IsNullOrWhiteSpace(tblOffer.SkillIdOfferOwner)
                                        ? await GetOfferedSkillNames(tblOffer.SkillIdOfferOwner)
                                        : new List<string>(),
                                OfferOwnerId = tblOffer.UserId,
                                WillingSkills = tblOffer.WillingSkill?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>()
                            };

                            ViewBag.OfferDetails = offerDisplay;
                        }
                    }

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
                            IsApproved = message.IsApproved,
                            OfferDetails = offerDisplay,
                            MessageType = messageType,
                            ApprovedByAdminId = message.ApprovedByAdminId,
                            ApprovedDate = message.ApprovedDate,
                            ApprovedByAdminName = message.ApprovedByAdmin?.UserName
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

        #region Delete Conversation

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConversation(int otherUserId)
        {
            int currentUserId = GetUserId();

            // ❶ Load the other user’s info
            var otherUser = await _context.TblUsers
                .Where(u => u.UserId == otherUserId)
                .Select(u => new { u.UserName })
                .FirstOrDefaultAsync();

            // ❷ Fetch all messages
            var msgs = await _context.TblMessages
                .Where(m =>
                    (m.SenderUserId == currentUserId && m.ReceiverUserId == otherUserId) ||
                    (m.SenderUserId == otherUserId && m.ReceiverUserId == currentUserId))
                .ToListAsync();

            if (!msgs.Any())
            {
                TempData["ErrorMessage"] = "No conversation found to delete.";
                return RedirectToAction("Conversation", new { otherUserId });
            }

            // ❸ Soft-delete
            foreach (var m in msgs)
            {
                m.IsDeleted = true;
                m.DeletedOn = DateTime.UtcNow;
                m.DeletedByUserId = currentUserId;
            }
            await _context.SaveChangesAsync();

            // ❹ Use their actual name in your confirmation
            var name = otherUser?.UserName ?? "that user";
            TempData["SuccessMessage"] = $"Your conversation with {name} has been deleted.";

            return RedirectToAction("Conversation", new { otherUserId = (int?)null });
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
        private async Task<List<string>> GetOfferedSkillNames(string skillIds)
        {
            if (string.IsNullOrWhiteSpace(skillIds))
                return new List<string>();

            var offeredSkillIds = skillIds
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .ToList();

            var allSkills = await _context.TblSkills.ToListAsync();
            return allSkills
                .Where(s => offeredSkillIds.Contains(s.SkillId))
                .Select(s => s.SkillName)
                .ToList();
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