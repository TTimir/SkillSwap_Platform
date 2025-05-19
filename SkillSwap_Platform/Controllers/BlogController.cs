using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.Blogs;

namespace SkillSwap_Platform.Controllers
{
    [Route("Blog")]
    public class BlogController : Controller
    {
        private readonly IBlogService _blog;
        private readonly ILogger<BlogController> _logger;
        private const int PageSize = 8;

        public BlogController(IBlogService blog, ILogger<BlogController> logger)
        {
            _blog = blog;
            _logger = logger;
        }

        // Primary feed page
        [HttpGet("Feed")]
        public IActionResult Feed() => View();

        // JSON endpoint to stream pages
        [HttpGet("FeedData")]
        public async Task<IActionResult> FeedData(int page = 1)
        {
            if (page < 1) page = 1;
            try
            {
                var paged = await _blog.ListAsync(page, PageSize);

                var posts = paged.Items;

                // Map to lightweight DTO for the feed
                // project to an anonymous type with camelCase props
                var dto = posts.Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Summary,
                    p.CreatedAt,
                    tags = p.Tags,
                    url = Url.Action("Details", "Blog", new { id = p.Id }),
                    coverImageUrl = string.IsNullOrWhiteSpace(p.CoverImagePath)
                              ? null
                              : Url.Content($"~/{p.CoverImagePath.TrimStart('/')}"),
                    authorName = "Timir Bhingradiya"
                });
                return Json(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming blog feed (page {Page})", page);
                return StatusCode(500, "Unable to load posts");
            }
        }

        // Full detail remains if someone clicks through
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                // main post
                var post = await _blog.GetByIdAsync(id);
                if (post == null) return NotFound();

                // recent 5 posts (excluding the current one)
                var pagedRecent = await _blog.ListAsync(1, 6);
                var recent = pagedRecent.Items
                                .Where(p => p.Id != id)
                                .Take(5)
                                .ToList();

                // pass both via a view-model
                var vm = new BlogDetailsVM
                {
                    Post = post,
                    RecentPosts = recent
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading blog post {PostId}", id);
                return RedirectToAction("Feed");
            }
        }
    }
}
