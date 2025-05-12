namespace SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel
{
    public class SearchCriteria
    {
        public string? Term { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
