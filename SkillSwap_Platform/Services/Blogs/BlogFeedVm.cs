using Microsoft.Extensions.Hosting;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Blogs
{
    public class BlogFeedVm
    {
        public IEnumerable<BlogPost> Posts { get; set; } = Enumerable.Empty<BlogPost>();
        public IEnumerable<BlogPost> RecentPosts { get; set; } = Enumerable.Empty<BlogPost>();
        public List<(string Tag, int Count)> TopTags { get; set; } = new();
        public string? SelectedTag { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
    }
}
