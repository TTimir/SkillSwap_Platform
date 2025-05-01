using SkillSwap_Platform.Services.AdminControls.Offer_and_Review.ViewModels;

namespace SkillSwap_Platform.Services.AdminControls.Offer_and_Review
{
    public interface IOfferReviewService
    {
        // Offers
        Task<PagedResult<OfferFlagVm>> GetFlaggedOffersAsync(int page, int pageSize);
        Task<FlaggedOfferDetailsVm> GetOfferDetailsAsync(int offerId);

        // Reviews
        Task<PagedResult<ActiveFlagVm>> GetActiveFlagsAsync(int page, int pageSize);
        Task<PagedResult<ReviewFlagVm>> GetFlaggedReviewsAsync(int page, int pageSize);
        Task<FlaggedReviewDetailsVm> GetFlaggedReviewDetailsAsync(int reviewId);
        Task<PagedResult<ReviewFlagVm>> GetFlaggedReplyFlagsAsync(int page, int pageSize);
        Task<FlaggedReviewDetailsVm> GetReplyDetailsAsync(int replyId);

        Task DismissOfferFlagsAsync(int offerId, int adminId, string notes);
        Task DismissReviewFlagsAsync(int reviewId, int flagId, int adminId, string notes);
        Task DismissReplyFlagsAsync(int replyId, int adminId, string notes);

        /// <summary>
        /// Delete a flagged review (or reply) and send a warning to its author.
        /// </summary>
        Task ModerateAndWarnAsync(
            int contentId,
            bool isReply,
            int adminId,
            string moderationNote,    // for your logs
            string warningMessage     // emailed to the user
        );

        // Services/AdminControls/Offer_and_Review/IOfferReviewService.cs
        Task<PagedResult<FlagHistoryVm>> GetFlagHistoryAsync(int page, int pageSize);
    }
}
