namespace SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel
{
    public class UserSearchResultDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsHeld { get; set; }
        public int FailedOtpAttempts { get; set; }
        public int TotalFlags { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
