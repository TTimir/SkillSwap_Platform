namespace SkillSwap_Platform.Models.ViewModels
{
    public class UserProfileVM
    {
        // Main user info
        public TblUser User { get; set; }

        // Related collections
        public IEnumerable<TblEducation> Educations { get; set; }
        public IEnumerable<TblExperience> Experiences { get; set; }
        public IEnumerable<TblUserSkill> UserSkills { get; set; }
        public IEnumerable<TblLanguage> Languages { get; set; }
        public IEnumerable<TblUserCertificate> Certificates { get; set; }

        // Additional skill summary fields from TblUsers.
        public string? DesiredSkillAreas { get; set; }
        public string? OfferedSkillAreas { get; set; }
        public bool IsOnline { get; set; }
        public double OverallLanguageLevel { get; set; }
        // New property for activity level
        public string ActivityLevel { get; set; }

    }
}
