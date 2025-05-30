using Microsoft.Extensions.Hosting;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Blogs
{
    public interface IBlogService
    {
        Task<PagedResult<BlogPost>> ListAsync(int page, int pageSize);
        Task<PagedResult<BlogPost>> ListByTagAsync(string tag, int page, int pageSize);

        Task<BlogPost> GetByIdAsync(int id);
        Task<BlogPost> CreateAsync(CreateBlogPostDto dto);
        Task<BlogPost> UpdateAsync(EditBlogPostDto dto);
        Task DeleteAsync(int id);
    }
}
