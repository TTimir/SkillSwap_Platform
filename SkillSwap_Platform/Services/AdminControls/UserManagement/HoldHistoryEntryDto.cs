namespace SkillSwap_Platform.Services.AdminControls.UserManagement
{
    public class HoldHistoryEntryDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public DateTime HeldAt { get; set; }
        public string HeldCategory { get; set; }
        public string HeldReason { get; set; }
        public DateTime? HeldUntil { get; set; }
        public string HeldByAdmin { get; set; }
        public DateTime? ReleaseAt { get; set; }
        public string ReleaseReason { get; set; }
        public string ReleasedByAdmin { get; set; }
    }

    public class HoldHistoryViewModel
    {
        public List<HoldHistoryEntryDto> Entries { get; set; }
    }
}
