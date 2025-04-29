namespace SkillSwap_Platform.Models.ViewModels.ReviewReplyVm
{
    public class ReviewVm
    {
        public int ReviewId { get; set; }
        public string ReviewerName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public double Rating { get; set; }
        public string Comments { get; set; } = "";
        public List<ReviewReplyVm> Replies { get; set; } = new();
    }
}
