using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class ContactFormVM
    {
        [Required] 
        public string Name { get; set; }
        [Required, EmailAddress] 
        public string Email { get; set; }
        public string Phone { get; set; }
        [Required] 
        public string Subject { get; set; }
        [Required] 
        public string Category { get; set; }
        [Required] 
        public string Message { get; set; }

        public IFormFile? Attachment { get; set; }

        // These three get populated by the controller
        public byte[]? AttachmentData { get; set; }
        public string? AttachmentFilename { get; set; }
        public string? AttachmentContentType { get; set; }
    }
}
