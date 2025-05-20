namespace SkillSwap_Platform.Services.Blogs
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    }
}
