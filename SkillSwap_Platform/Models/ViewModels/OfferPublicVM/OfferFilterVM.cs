using Microsoft.AspNetCore.Mvc.Rendering;

namespace SkillSwap_Platform.Models.ViewModels.OfferFilterVM
{
    public class OfferFilterVM
    {
        // Filters
        public string Category { get; set; }
        public int? SkillId { get; set; }
        public int? MinTokenCost { get; set; }
        public int? MaxTokenCost { get; set; }
        public string Keyword { get; set; }

        public string FilterCategory { get; set; }
        public string FilterLanguage { get; set; }
        public string FilterLocation { get; set; }
        public string FilterSkills { get; set; }
        public string DesignTool { get; set; }
        public string FreelanceType { get; set; }
        public string InteractionMode { get; set; }
        public string SkillLevel { get; set; }

        public int? MaxTimeCommitment { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;

        // Results
        public IEnumerable<TblReview> Reviews { get; set; }
        public List<OfferCardVM> Offers { get; set; } = new();

        public List<SelectListItem> CategoryOptions { get; set; } = new();
        public List<SelectListItem> SkillOptions { get; set; } = new();
        public List<SelectListItem> LanguageOptions { get; set; } = new();
        public List<SelectListItem> LocationOptions { get; set; } = new();
        public List<SelectListItem> DesignToolOptions { get; set; } = new();
        public List<SelectListItem> SkillLevelOptions { get; set; } = new();
        public List<SelectListItem> TimeCommitmentOptions { get; set; } = new();
        public List<SelectListItem> FreelanceTypeOptions { get; set; } = new();
    }
}
