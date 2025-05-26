using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.HelperClass
{
    public class CertificateDocument : IDocument
    {
        private readonly CertificateVM _vm;

        public CertificateDocument(CertificateVM vm)
        {
            _vm = vm;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.Letter);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(14));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        void ComposeHeader(IContainer header)
        {
            header.Row(row =>
            {
                row.RelativeItem().AlignLeft().Height(50)
                    .Image(_vm.LogoUrl, ImageScaling.FitHeight);
                row.ConstantItem(200).AlignRight().Text("Certificate of Completion")
                   .FontSize(24).SemiBold().FontColor(Colors.Blue.Medium);
            });
        }

        void ComposeFooter(IContainer footer)
        {
            footer.PaddingTop(30).Column(col =>
            {
                // 1) Signature image (if provided)
                if (!string.IsNullOrEmpty(_vm.SignatureUrl))
                {
                    col.Item()
                    .AlignCenter()
                    .Height(120)
                    .Image(PlaceImage(_vm.SignatureUrl), ImageScaling.FitHeight);
                }

                // 2) Recipient name underneath
                col.Item()
                   .AlignCenter()
                   .Text(_vm.RecipientName)
                   .FontSize(12)
                   .FontColor(Colors.Grey.Darken2);
            });
        }

        void ComposeContent(IContainer content)
        {
            content.PaddingVertical(20).Column(col =>
            {
                col.Spacing(10);

                col.Item().Text($"This certifies that").AlignCenter();
                col.Item().Text(_vm.RecipientName)
                      .AlignCenter().FontSize(20).Bold();
                col.Item().Text($"has successfully completed the session")
                      .AlignCenter();
                col.Item().Text($"“{_vm.SessionTitle}”")
                      .AlignCenter().Italic();
                col.Item().Text($"on {_vm.CompletedAt:MMMM d, yyyy}")
                      .AlignCenter();
            });
        }


        private byte[] PlaceImage(string urlOrPath)
        {
            // remove leading “~/”
            var rel = urlOrPath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
            var physical = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                rel
            );
            return System.IO.File.ReadAllBytes(physical);
        }
    }
}