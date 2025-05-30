using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
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
        public async Task<IActionResult> Feed(string? tag, int page = 1)
        {
            // Recent posts for sidebar
            var recentPaged = await _blog.ListAsync(1, 11);
            var recent = recentPaged.Items.Take(10).ToList();

            // Top tags (across all posts)
            var allPosts = await _blog.ListAsync(1, int.MaxValue);
            var tagCounts = allPosts.Items
                .SelectMany(p => p.Tags)
                .GroupBy(t => t)
                .Select(g => (Tag: g.Key, Count: g.Count()))
                .OrderByDescending(tc => tc.Count)
                .Take(20)
                .ToList();

            // Page through either the whole feed or just that tag
            var feedPaged = string.IsNullOrWhiteSpace(tag)
                ? await _blog.ListAsync(page, PageSize)
                : await _blog.ListByTagAsync(tag, page, PageSize);

            // Build view model
            var vm = new BlogFeedVm
            {
                Posts = feedPaged.Items,
                RecentPosts = recent,
                TopTags = tagCounts,
                SelectedTag = tag,
                Page = page,
                TotalPages = (int)Math.Ceiling(feedPaged.TotalItems / (double)PageSize)
            };

            return View(vm);
        }

        // JSON endpoint to stream pages
        [HttpGet("FeedData")]
        public async Task<IActionResult> FeedData(int page = 1, string? tag = null)
        {
            if (page < 1) page = 1;
            try
            {
                var paged = string.IsNullOrWhiteSpace(tag)
                    ? await _blog.ListAsync(page, PageSize)
                    : await _blog.ListByTagAsync(tag, page, PageSize);

                var dto = paged.Items.Select(p => new {
                    p.Id,
                    p.Title,
                    p.Summary,
                    p.CreatedAt,
                    tags = p.Tags,
                    url = Url.Action("Details", "Blog", new { id = p.Id }),
                    coverImageUrl = string.IsNullOrWhiteSpace(p.CoverImagePath)
                                      ? null
                                      : Url.Content($"~/{p.CoverImagePath.TrimStart('/')}"),
                    authorName = "Swapo Author"
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

                // pass both via a view-model
                var vm = new BlogDetailsVM
                {
                    Post = post,
                    Author = "Swapo Author"
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
