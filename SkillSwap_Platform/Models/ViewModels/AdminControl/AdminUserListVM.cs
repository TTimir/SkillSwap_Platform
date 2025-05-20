namespace SkillSwap_Platform.Models.ViewModels.AdminControl
{
    public class AdminUserListVM
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public List<string> Roles { get; set; } = new();
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsHeld { get; set; }
    }
}
