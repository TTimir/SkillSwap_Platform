namespace SkillSwap_Platform.Models.ViewModels.ReviewReplyVm
{
    public class ReviewReplyVm
    {
        public int ReplyId { get; set; }
        public string RepliedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Text { get; set; } = "";
    }
}
