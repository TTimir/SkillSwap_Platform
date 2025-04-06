using Microsoft.AspNetCore.Mvc.Rendering;

namespace SkillSwap_Platform.Models.ViewModels.FreelancersVM
{
    public class UserProfileListVM
    {
        public List<ProfileCardVM> Profiles { get; set; }
        public string Keyword { get; set; }
        public string Location { get; set; }
        public string Designation { get; set; }   // Selected designation filter value
        public string FreelanceType { get; set; }
        public string InteractionMode { get; set; }
        public string Skill { get; set; }
        public string FilterLanguage { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string Category { get; set; }

        // Optional: Add filter options for dropdowns if needed
        public List<SelectListItem> LocationOptions { get; set; } = new();
        public List<SelectListItem> DesignationOptions { get; set; }
        public List<SelectListItem> FreelanceTypeOptions { get; set; }
        public List<SelectListItem> CategoryOptions { get; set; }
        public List<SelectListItem> InteractionModeOptions { get; set; }
        public List<SelectListItem> SkillOptions { get; set; }
        public List<SelectListItem> LanguageOptions { get; set; }
    }
}
