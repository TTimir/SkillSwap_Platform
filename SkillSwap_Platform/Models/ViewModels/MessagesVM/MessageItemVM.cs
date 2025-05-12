using System;

namespace SkillSwap_Platform.Models.ViewModels.MessagesVM
{
    public class MessageItemVM
    {
        public int MessageId { get; set; }
        public int CurrentUserID { get; set; }
        public int SenderUserID { get; set; }
        public string SenderName { get; set; }
        public string SenderProfileImage { get; set; }
        public DateTime SentDate { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; } = "Normal";
        public int? ResourceId { get; set; }
        public bool IsRead { get; set; }
        public bool ShowHeader { get; set; }
        public MessageStatus Status { get; set; }
        public string? OfferedSkillName { get; set; }
        public string? ReceiverSkillName { get; set; }
        public int? ReplyMessageId { get; set; }
        public string ReplyPreview { get; set; }
        public bool IsFlagged { get; set; }
        public bool IsApproved { get; set; } = true;
        public IEnumerable<TblMessageAttachment> Attachments { get; set; } = new List<SkillSwap_Platform.Models.TblMessageAttachment>();

        // Computed property: true if the current user sent this message.
        public bool IsSentByCurrent => SenderUserID == CurrentUserID;

        public bool IsMeetingCard { get; set; }
        public string MeetingTitle { get; set; }
        public string MeetingLink { get; set; }

        public int? OfferId { get; set; }
        public OfferDisplayVM OfferDetails { get; set; }

        public int ExchangeId { get; set; }

        public TblExchange? Exchange { get; set; }
        public TblInPersonMeeting? InPersonMeeting { get; set; }

        public string ExchangeOfferOwnerName { get; set; }
        public string ExchangeOtherUserName { get; set; }

        public TblContract? ContractDetails { get; set; }
        public bool IsContractPending => ContractDetails?.Status == "Pending";

        /// <summary>Who (if anyone) approved this held message</summary>
        public int? ApprovedByAdminId { get; set; }

        /// <summary>When they approved it</summary>
        public DateTime? ApprovedDate { get; set; }

        /// <summary>The approving admin’s username (optional nav)</summary>
        public string? ApprovedByAdminName { get; set; }

    }

    public enum MessageStatus
    {
        Sending,
        Delivered, // delivered but not read yet
        Seen,      // delivered and read (seen)
        Failed
    }

}
