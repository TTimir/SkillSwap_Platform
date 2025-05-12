namespace SkillSwap_Platform.Services.AdminControls
{
    public class OtpUserSummaryVm
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public int FailedCount { get; set; }
        public DateTime LastAttemptAt { get; set; }
        public bool HasRecentFailure { get; set; }
    }
}
