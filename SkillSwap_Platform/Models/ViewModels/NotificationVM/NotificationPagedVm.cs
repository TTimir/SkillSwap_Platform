namespace SkillSwap_Platform.Models.ViewModels.NotificationVM
{
    public class NotificationPagedVm
    {
        public IReadOnlyList<NotificationTrackVm> Items { get; set; } = Array.Empty<NotificationTrackVm>();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages
            => (int)Math.Ceiling(TotalCount / (double)PageSize);

        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
        public string? SearchTerm { get; set; }
    }
}
