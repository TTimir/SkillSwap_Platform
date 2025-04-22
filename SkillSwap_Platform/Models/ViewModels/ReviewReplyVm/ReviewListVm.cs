namespace SkillSwap_Platform.Models.ViewModels.ReviewReplyVm
{
    public class ReviewListVm
    {
        public int OfferId { get; set; }
        public List<TblReview> Reviews { get; set; } = new();
    }
}
