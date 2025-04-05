using PuppeteerSharp.Media;
using PuppeteerSharp;

namespace SkillSwap_Platform.Services.PDF
{
    public class PuppeteerPdfGenerator : IPdfGenerator
    {
        public async Task<byte[]> GeneratePdfFromHtmlAsync(string htmlContent, int contractVersion)
        {
            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions 
            {
                Headless = true, 
                Args = new[] { "--no-sandbox" } 
            });
            using var page = await browser.NewPageAsync();

            await page.SetContentAsync(htmlContent, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Load }
            });

            var pdfOptions = new PdfOptions
            {
                Format = PaperFormat.A4,       // or whichever format you already use
                PrintBackground = true,        // preserve any background color/images
                DisplayHeaderFooter = true,    // this must be true to show headers/footers

                // Adjust margins so the footer doesn’t overlap contract text
                MarginOptions = new MarginOptions
                {
                    Top = "40px",
                    Bottom = "60px",
                    Left = "20px",
                    Right = "20px"
                },

                // If you don't need a header, set a minimal or empty div
                HeaderTemplate = "<div></div>",

                // Footer: placeholders for page # & total pages, plus a tiny script for "Continue"/"End"
                // Footer: includes:
                //  1) Legal text
                //  2) Version: {contractVersion}
                //  3) Page X of Y
                //  4) "Continue to next page" or "End" on the last page
                FooterTemplate = $@"
                    <div style='font-size:10px; text-align:center; width:100%; line-height:1.3;'>
                        Please review carefully. (Version: {contractVersion})<br/>
                        Page <span class='pageNumber'></span> of <span class='totalPages'></span>
                        <br/>
                        <span id='footerText'></span>
                    </div>
                    <script>
                    (function() {{
                        // Because Puppeteer doesn't always re-run the script after 
                        // it inserts .pageNumber / .totalPages text, 
                        // reading them here can be unreliable. 
                        // If you do see them come out empty, you can use 
                        // a setTimeout(...) hack to wait briefly. 
                        setTimeout(function() {{
                            var pageNumElem  = document.querySelector('.pageNumber');
                            var totalNumElem = document.querySelector('.totalPages');
                            if(!pageNumElem || !totalNumElem) return;
                    
                            var pageNum  = parseInt(pageNumElem.innerText, 10);
                            var totalNum = parseInt(totalNumElem.innerText, 10);
                    
                            var footerSpan = document.getElementById('footerText');
                            footerSpan.innerText = (pageNum < totalNum) ? 'Continue to next page' : 'End';
                        }}, 50);
                    }})();
                    </script>"
            };

            var pdfBytes = await page.PdfDataAsync(pdfOptions);

            return pdfBytes;
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