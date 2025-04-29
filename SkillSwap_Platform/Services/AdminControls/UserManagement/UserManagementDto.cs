namespace SkillSwap_Platform.Services.AdminControls.UserManagement
{
    public class UserManagementDto
    {
        public int UserId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool IsHeld { get; set; }
        public DateTime? HeldAt { get; set; }
        public string HeldReason { get; set; }
        public string HeldCategory { get; set; }
        public DateTime? HeldUntil { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string? ReleaseReason { get; set; }
        public int? ReleasedByAdmin { get; set; }
        public int TotalHolds { get; set; }
    }
}
