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

        [DataType(DataType.Upload)]
        [FileExtensions(Extensions = "jpg,jpeg,png,pdf",
            ErrorMessage = "Allowed file types: .jpg, .jpeg, .png, .pdf")]
        public IFormFile? Attachment { get; set; }

        // These three get populated by the controller
        public byte[]? AttachmentData { get; set; }
        public string? AttachmentFilename { get; set; }
        public string? AttachmentContentType { get; set; }
    }
}
