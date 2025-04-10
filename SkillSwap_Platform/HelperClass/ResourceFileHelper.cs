namespace SkillSwap_Platform.HelperClass
{
    public class ResourceFileHelper
    {
        public static async Task<string> UploadFileAsync(IFormFile file, string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            string fullFilePath = Path.Combine(folderPath, uniqueFileName);
            try
            {
                using (var stream = new FileStream(fullFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                // Create a relative file URL for accessing later.
                string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot") + Path.DirectorySeparatorChar;
                string relativePath = fullFilePath.Replace(wwwrootPath, string.Empty).Replace("\\", "/");
                return "/" + relativePath;
            }
            catch (Exception ex)
            {
                throw new Exception("File upload failed.", ex);
            }
        }
    }
}