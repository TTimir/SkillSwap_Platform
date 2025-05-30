using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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

        public async Task<PagedResult<BlogPost>> ListAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;

            var query = _db.TblBlogPosts.AsNoTracking();

            var totalItems = await query.CountAsync();
            var entities = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var models = entities.Select(MapEntity).ToList();

            return new PagedResult<BlogPost>
            {
                Items = models,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<BlogPost>> ListByTagAsync(string tag, int page, int pageSize)
        {
            if (page < 1) page = 1;

            // 1) Grab all posts (only the columns we need)
            var raw = await _db.TblBlogPosts
                .AsNoTracking()
                .Select(e => new {
                    e.Id,
                    e.Title,
                    e.Summary,
                    e.CreatedAt,
                    e.CoverImagePath,
                    e.Tags             // comma-delimited
                })
                .ToListAsync();

            // 2) Split & trim each Tags field, then filter
            var matching = raw
                .Select(e => new {
                    e.Id,
                    e.Title,
                    e.Summary,
                    e.CreatedAt,
                    e.CoverImagePath,
                    TagList = string.IsNullOrWhiteSpace(e.Tags)
                        ? Array.Empty<string>()
                        : e.Tags
                             .Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(t => t.Trim())
                             .ToArray()
                })
                .Where(e => e.TagList
                             .Any(t =>
                                 t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // 3) Remember the total for pagination
            var totalItems = matching.Count;

            // 4) Page that filtered list in memory
            var pageItems = matching
                .OrderByDescending(e => e.CreatedAt)   // keep the same default sort
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 5) Map into your BlogPost DTO
            var models = pageItems
                .Select(e => new BlogPost
                {
                    Id = e.Id,
                    Title = e.Title,
                    Summary = e.Summary,
                    CreatedAt = e.CreatedAt,
                    CoverImagePath = e.CoverImagePath,
                    Tags = e.TagList.ToList()
                })
                .ToList();

            return new PagedResult<BlogPost>
            {
                Items = models,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }

        public Task<BlogPost?> GetByIdAsync(int id)
        {
            return _db.TblBlogPosts
                .Where(e => e.Id == id)
                .Select(e => new BlogPost
                {
                    Id = e.Id,
                    Title = e.Title,
                    Summary = e.Summary,
                    CreatedAt = e.CreatedAt,
                    CoverImagePath = e.CoverImagePath,
                    Tags = string.IsNullOrWhiteSpace(e.Tags)
                        ? new List<string>()
                        : e.Tags
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .ToList()
                })
                .FirstOrDefaultAsync();
        }

        // map EF entity → your DTO/model
        private static BlogPost MapEntity(TblBlogPost e) => new BlogPost
        {
            Id = e.Id,
            Title = e.Title,
            Summary = e.Summary,
            CreatedAt = e.CreatedAt,
            CoverImagePath = e.CoverImagePath,
            Tags = string.IsNullOrWhiteSpace(e.Tags)
                               ? new List<string>()
                               : e.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(t => t.Trim())
                                       .ToList()
        };

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