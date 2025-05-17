using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Blogs;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class AdminBlogController : Controller
    {
        private readonly IBlogService _blog;

        public AdminBlogController(IBlogService blog)
        {
            _blog = blog;
        }

        // GET: /Admin/Blog
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 9;
            var posts = await _blog.ListAsync(page, pageSize);
            return View(posts);
        }

        // GET: /Admin/Blog/Create
        public IActionResult Create()
        {
            var dto = new CreateBlogPostDto
            {
                AuthorId = GetCurrentUserId()  // your helper
            };
            return View(dto);
        }

        // POST: /Admin/Blog/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBlogPostDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                await _blog.CreateAsync(dto);
                TempData["SuccessMessage"] = "Blog post created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Failed to create blog post. Please try again.";
                return View(dto);
            }
        }

        // GET: /Admin/Blog/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _blog.GetByIdAsync(id);
            if (entity == null)
                return NotFound();

            // map EF model → DTO
            var dto = new EditBlogPostDto
            {
                Id = entity.Id,
                AuthorId = entity.AuthorId,
                Title = entity.Title,
                Summary = entity.Summary,
                Content = entity.Content
            };

            return View(dto);
        }

        // POST: /Admin/Blog/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditBlogPostDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                await _blog.UpdateAsync(dto);
                TempData["SuccessMessage"] = "Blog post updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                ViewBag.ErrorMessage = $"No blog post found with ID {dto.Id}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Failed to update blog post. Please try again.";
                return View(dto);
            }
        }

        // POST: /Admin/Blog/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _blog.DeleteAsync(id);
                TempData["SuccessMessage"] = "Blog post deleted.";
            }
            catch (KeyNotFoundException)
            {
                ViewBag.ErrorMessage = $"Blog post {id} not found.";
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Failed to delete blog post.";
            }
            return RedirectToAction(nameof(Index));
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

    }
}
