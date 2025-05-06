using SkillSwap_Platform.Services.AdminControls;
using SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel;

namespace SkillSwap_Platform.Models.ViewModels.AdminSearch
{
    public class OfferSearchVM
    {
        public string Term { get; set; } = "";
        public PagedResult<OfferSearchResultDto> Results { get; set; } = new();
    }
}
