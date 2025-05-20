namespace SkillSwap_Platform.Models.ViewModels.AdminControl
{
    public class AdminUserIndexVM
    {
        public List<AdminUserListVM> Users { get; set; } = new();
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
    }
}
