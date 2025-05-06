using SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel;

namespace SkillSwap_Platform.Models.ViewModels.AdminSearch
{
    public class OfferDetailVM
    {
        public OfferDetailDto offer { get; }
        public OfferDetailVM(OfferDetailDto dto) => offer = dto;
    }
}
