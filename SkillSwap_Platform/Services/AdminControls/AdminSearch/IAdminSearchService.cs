using SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel;

namespace SkillSwap_Platform.Services.AdminControls.AdminSearch
{
    public interface IAdminSearchService
    {
        Task<PagedResult<OfferSearchResultDto>> SearchOffersAsync(SearchCriteria criteria);
        Task<OfferDetailDto> GetOfferDetailAsync(int offerId, int currentUserId);
        Task<PagedResult<UserSearchResultDto>> SearchUsersAsync(SearchCriteria criteria);
        Task<UserDetailDto> GetUserDetailAsync(int userId);
    }
}
