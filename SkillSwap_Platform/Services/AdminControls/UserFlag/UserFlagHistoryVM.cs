namespace SkillSwap_Platform.Services.AdminControls.UserFlag
{
    public class UserFlagHistoryVM
    {
        public int UserId { get; set; }
        public string UserName { get; set; }

        /// <summary>
        /// All of the actions (flag, dismiss, remove) taken on this user,
        /// ordered by date descending.
        /// </summary>
        public IReadOnlyList<UserFlagHistoryItem> History { get; set; }
    }

    public class UserFlagHistoryItem
    {
        public int FlagId { get; set; }
        public string FlaggedBy { get; set; }
        public DateTime FlaggedDate { get; set; }

        /// <remarks>
        /// Will be `null` until an admin takes action.
        /// </remarks>
        public string ActionTaken { get; set; }    // “Dismissflag” or “RemoveUser” or “Warn1”/“Warn2”
        public string? AdminUser { get; set; }
        public DateTime? ActionDate { get; set; }
        public string? AdminReason { get; set; }
    }
}