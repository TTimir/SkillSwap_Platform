using Microsoft.AspNetCore.Mvc.Rendering;

namespace SkillSwap_Platform.Models.ViewModels.FreelancersVM
{
    public class UserProfileListVM
    {
        // Filters
        public string Keyword { get; set; }
        public string Location { get; set; }

        // Pagination
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        // Results
        public List<ProfileCardVM> Profiles { get; set; } = new();

        // Optional: Add filter options for dropdowns if needed
        public List<SelectListItem> LocationOptions { get; set; } = new();
    }
}
