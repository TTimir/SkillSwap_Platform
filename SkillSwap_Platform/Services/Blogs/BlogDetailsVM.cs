using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Blogs
{
    public class BlogDetailsVM
    {
        public string Author { get; set; }
        public BlogPost Post { get; set; }
        public List<BlogPost> RecentPosts { get; set; }
    }
}
