namespace SkillSwap_Platform.Services.PDF
{
    public interface IPdfGenerator
    {
        Task<byte[]> GeneratePdfFromHtmlAsync(string htmlContent, int contractVersion);
        Task<string> SavePdfToDiskAsync(string htmlContent, string fileName);
    }
}
