using SkillSwap_Platform.Models.ViewModels.ReviewReplyVm;

namespace SkillSwap_Platform.Services.ReviewReply
{
    public interface IReviewService
    {
        Task<ReviewListVm> GetReviewsAsync(int offerId);
        Task<ReviewReplyVm> AddReplyAsync(int reviewId, string text, int userId);
    }
}
