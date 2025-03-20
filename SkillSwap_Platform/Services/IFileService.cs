namespace SkillSwap_Platform.Services
{
    public interface IFileService
    {
        bool ValidateFile(IFormFile file, string[] allowedExtensions, long maxSizeBytes, out string errorMessage);
        Task<string> UploadFileAsync(IFormFile file, string folderName);
    }
}
