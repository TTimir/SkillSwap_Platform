using SkillSwap_Platform.Models.ViewModels.ExchangeVM;

namespace SkillSwap_Platform.Models.ViewModels.UserProfileMV
{
    public class UserProfileVM
    {
        // Main user info
        public TblUser User { get; set; }

        public string Category { get; set; } = "";

        // Related collections
        public IEnumerable<TblEducation> Educations { get; set; }
        public IEnumerable<TblExperience> Experiences { get; set; }
        public IEnumerable<TblLanguage> Languages { get; set; }
        public IEnumerable<CertificateVM> Certificates { get; set; }

        public IEnumerable<SkillVM> Skills { get; set; }
        public List<OfferDetailsVM> Offers { get; set; }
        public int LastExchangeDays { get; set; }
        public double TotalYearsOfExperience { get; set; }
        public double RecommendedPercentage { get; set; }
        public int TotalExchanges { get; set; }
        public string AverageResponseTime { get; set; }
        public int ActiveExchangeCount { get; set; }
        public IEnumerable<TblReview> Reviews { get; set; }
        public int ReviewCount { get; set; }
        public double AverageRating { get; set; }
        public bool IsFlagged { get; set; }
        public bool IsOwnProfile { get; set; }
    }

    public class SkillVM
    {
        public string Name { get; set; }
        public bool IsOffered { get; set; }
    }
}
