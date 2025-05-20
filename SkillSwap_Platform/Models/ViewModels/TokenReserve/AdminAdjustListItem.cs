namespace SkillSwap_Platform.Models.ViewModels.TokenReserve
{
    public class AdminAdjustListItem
    {
        public int TransactionId { get; set; }
        public string UserName { get; set; }
        public decimal Amount { get; set; }
        public bool IsCredit => ToUserId.HasValue;
        public string AdminAdjustmentType { get; set; }
        public string AdminAdjustmentReason { get; set; }
        public decimal OldBalance { get; set; }
        public decimal NewBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool RequiresApproval { get; set; }
        public bool? IsApproved { get; set; }

        // For paging only
        public int? ToUserId { get; set; }
        public int? FromUserId { get; set; }
    }

    // Models/ViewModels/AdminAdjustListViewModel.cs
    public class AdminAdjustListViewModel
    {
        public List<AdminAdjustListItem> Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    }
}
