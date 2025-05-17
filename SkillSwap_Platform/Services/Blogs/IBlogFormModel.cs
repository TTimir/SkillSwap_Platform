namespace SkillSwap_Platform.Services.Blogs
{
    public interface IBlogFormModel
    {
        int Id { get; set; }
        int AuthorId { get; set; }
        string Title { get; set; }
        string Summary { get; set; }
        string Content { get; set; }
        public IFormFile? CoverImage { get; set; }
        public string Tags { get; set; }
    }
}
