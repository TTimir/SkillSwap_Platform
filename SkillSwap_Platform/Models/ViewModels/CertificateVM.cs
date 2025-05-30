using System.Numerics;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class CertificateVM
    {
        public string RecipientName { get; set; } = "";
        public string SessionTitle { get; set; } = "";
        public DateTime CompletedAt { get; set; }
        public string IssuerName { get; set; } = "Swapo Org";
        public string LogoUrl { get; set; } = "/template_assets/images/header-logo-dark.png";
        public string SignatureUrl { get; set; } = "/template_assets/images/signature.png";
    }
}
