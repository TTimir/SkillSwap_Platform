using SkillSwap_Platform.Models.ViewModels.ExchangeVM;

namespace SkillSwap_Platform.Models.ViewModels.UserProfileMV
{
    public class UserProfileVM
    {
        // Main user info
        public TblUser User { get; set; }

        // Related collections
        public IEnumerable<TblEducation> Educations { get; set; }
        public IEnumerable<TblExperience> Experiences { get; set; }
        public IEnumerable<TblLanguage> Languages { get; set; }
        public IEnumerable<TblUserCertificate> Certificates { get; set; }

        public IEnumerable<SkillVM> Skills { get; set; }
        public List<OfferDetailsVM> Offers { get; set; }
        public int LastExchangeDays { get; set; }
        public double TotalYearsOfExperience { get; set; }
        public double RecommendedPercentage { get; set; }
    }

    public class SkillVM
    {
        public string Name { get; set; }
        public bool IsOffered { get; set; }
    }
}
