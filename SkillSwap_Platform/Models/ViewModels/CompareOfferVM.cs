namespace SkillSwap_Platform.Models.ViewModels
{
    public class CompareOfferVM
    {
        public int UserId { get; set; }
        public int OfferId { get; set; }
        public string Title { get; set; }
        public string ShortTitle { get; set; } 
        public int TokenCost { get; set; }
        public int TimeCommitmentDays { get; set; }
        public string Category { get; set; }
        public string FreelanceType { get; set; }
        public string RequiredSkillLevel { get; set; }
        public string CollaborationMethod { get; set; }
        public int AssistanceRounds { get; set; }
        public string RecommendedPercentage { get; set; }
        public double JobSuccessRate { get; set; }
        public string Username { get; set; }
        public List<string> CompareWillingSkills { get; set; }
        public string ProfileImage { get; set; }

    }
}
