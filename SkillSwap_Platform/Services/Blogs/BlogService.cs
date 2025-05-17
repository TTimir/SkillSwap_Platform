using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Blogs
{
    public class BlogService : IBlogService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<BlogService> _logger;
        private readonly IWebHostEnvironment _env;

        public BlogService(SkillSwapDbContext db, ILogger<BlogService> logger, IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }

        public async Task<int> CountAsync()
            => await _db.TblBlogPosts.CountAsync();

        public async Task<IEnumerable<BlogPost>> ListAsync(int page, int pageSize)
        {
            return await _db.TblBlogPosts
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => MapToDto(x))
                .ToListAsync();
        }

        public async Task<BlogPost> GetByIdAsync(int id)
        {
            var entity = await _db.TblBlogPosts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id);
            return entity == null ? null : MapToDto(entity);
        }

        public async Task<BlogPost> CreateAsync(CreateBlogPostDto dto)
        {
            string imagePath = null;
            if (dto.CoverImage != null && dto.CoverImage.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "blogs");
                Directory.CreateDirectory(folder);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.CoverImage.FileName)}";
                var fullPath = Path.Combine(folder, fileName);
                using var stream = System.IO.File.Create(fullPath);
                await dto.CoverImage.CopyToAsync(stream);
                imagePath = $"/uploads/blogs/{fileName}";
            }

            var entity = new TblBlogPost
            {
                Title = dto.Title,
                Summary = dto.Summary,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow,
                AuthorId = dto.AuthorId,
                CoverImagePath = imagePath,
                Tags = dto.Tags
            };

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.TblBlogPosts.Add(entity);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("Created blog post {PostId}", entity.Id);
                return MapToDto(entity);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error creating blog post");
                throw;
            }
        }

        public async Task<BlogPost> UpdateAsync(EditBlogPostDto dto)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Use dto.Id here
                var entity = await _db.TblBlogPosts.FindAsync(dto.Id);
                if (entity == null)
                    throw new KeyNotFoundException($"Blog post {dto.Id} not found");

                // 1) optionally replace cover-image
                if (dto.CoverImage != null && dto.CoverImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "blogs");
                    Directory.CreateDirectory(uploadsFolder);

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.CoverImage.FileName)}";
                    var fullPath = Path.Combine(uploadsFolder, fileName);
                    using var fs = File.Create(fullPath);
                    await dto.CoverImage.CopyToAsync(fs);

                    entity.CoverImagePath = $"/uploads/blogs/{fileName}";
                }

                // Map the fields
                entity.Title = dto.Title;
                entity.Summary = dto.Summary;
                entity.Content = dto.Content;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.Tags = dto.Tags;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("Updated blog post {PostId}", dto.Id);

                // Return a DTO or your domain model
                return MapToDto(entity);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error updating blog post {PostId}", dto.Id);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var entity = await _db.TblBlogPosts.FindAsync(id);
                if (entity == null)
                    throw new KeyNotFoundException($"Blog post {id} not found");

                _db.TblBlogPosts.Remove(entity);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("Deleted blog post {PostId}", id);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error deleting blog post {PostId}", id);
                throw;
            }
        }

        // helper to map EF entity → DTO
        private static BlogPost MapToDto(TblBlogPost x) => new BlogPost
        {
            Id = x.Id,
            Title = x.Title,
            Summary = x.Summary,
            Content = x.Content,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            AuthorId = x.AuthorId,
            CoverImagePath = x.CoverImagePath,
            Tags = string.IsNullOrWhiteSpace(x.Tags)
                        ? new List<string>()
                        : x.Tags
                             .Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(t => t.Trim())
                             .Where(t => t.Length > 0)
                             .ToList()
        };
    }
}