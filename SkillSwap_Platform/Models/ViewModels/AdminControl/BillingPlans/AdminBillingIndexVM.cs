namespace SkillSwap_Platform.Models.ViewModels.AdminControl.BillingPlans
{
    public class AdminBillingIndexVM
    {
        public List<AdminSubscriptionItem> Subscriptions { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public string Term { get; set; }
    }
    public class AdminSubscriptionItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string UserEmail { get; set; }
        public string PlanName { get; set; }
        public string BillingCycle { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsAutoRenew { get; set; }
    }
    public class AdminCancellationIndexVM
    {
        public List<AdminCancellationItem> Requests { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }
    public class AdminCancellationItem
    {
        public int Id { get; set; }
        public int SubscriptionId { get; set; }
        public string Name { get; set; }
        public string UserEmail { get; set; }
        public string PlanName { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Reason { get; set; }
    }
    public class AdminHistoryVM
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
        public List<AdminHistoryItem> Timeline { get; set; }
    }
    public class AdminHistoryItem
    {
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
    }
    public class AdminUserListVM
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
        public List<AdminUserItem> Users { get; set; }
        public string Term { get; set; }
    }

    public class AdminUserItem
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string CurrentPlan { get; set; }
        public string CurrentCycle { get; set; }
    }
}
