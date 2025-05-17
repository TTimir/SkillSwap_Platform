using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Services.Blogs
{
    public class CreateBlogPostDto : IBlogFormModel
    {
        // Id = 0 for new posts
        public int Id { get; set; }

        // Will be set in controller from the current user
        public int AuthorId { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required, StringLength(500)]
        public string Summary { get; set; }

        [Required]
        public string Content { get; set; }
        public string Tags { get; set; }
        public IFormFile CoverImage { get; set; }
    }

    public class EditBlogPostDto : IBlogFormModel
    {
        public int Id { get; set; }

        public int AuthorId { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required, StringLength(500)]
        public string Summary { get; set; }

        [Required]
        public string Content { get; set; }
        public string Tags { get; set; }
        public IFormFile CoverImage { get; set; }
    }
}
