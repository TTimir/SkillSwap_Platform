namespace SkillSwap_Platform.Services.AdminControls.UserFlag
{
    public class FlaggedUserSummary
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public int TotalFlags { get; set; }
    }
}
