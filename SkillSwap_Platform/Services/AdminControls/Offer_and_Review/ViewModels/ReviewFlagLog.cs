namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels
{
    public class ReviewFlagLog
    {
        public int FlagId { get; set; }
        public int ReporterUserId { get; set; }
        public string ReporterUserName { get; set; } = "";
        public DateTime FlaggedAt { get; set; }
        public string Reason { get; set; } = "";
    }
}
