using PuppeteerSharp.Media;
using PuppeteerSharp;

namespace SkillSwap_Platform.Services.PDF
{
    public class PuppeteerPdfGenerator : IPdfGenerator
    {
        public async Task<byte[]> GeneratePdfFromHtmlAsync(string htmlContent)
        {
            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            return await page.PdfDataAsync(new PdfOptions { Format = PaperFormat.A4, PrintBackground = true });
        }

        public async Task<string> SavePdfToDiskAsync(string htmlContent, string fileName)
        {
            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            string pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "contracts");
            if (!Directory.Exists(pdfDirectory)) Directory.CreateDirectory(pdfDirectory);
            string filePath = Path.Combine(pdfDirectory, fileName);

            await page.PdfAsync(filePath, new PdfOptions { Format = PaperFormat.A4, PrintBackground = true });
            return $"/contracts/{fileName}";
        }
    }
}