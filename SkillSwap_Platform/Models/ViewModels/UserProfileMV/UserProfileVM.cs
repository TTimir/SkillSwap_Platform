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

        public List<SkillVM> Skills { get; set; }
        public int LastExchangeDays { get; set; }
        public double TotalYearsOfExperience { get; set; }
        public double RecommendedPercentage { get; set; }
        public bool IsOnline
        {
            get
            {
                if (User == null || User.LastActive == null)
                    return false; // If there's no record of activity, assume offline

                // Calculate the time difference between now and the user's last activity
                TimeSpan timeSinceLastActive = DateTime.UtcNow - User.LastActive.Value;

                // If the last activity was within the last 10 minutes, the user is online
                return timeSinceLastActive.TotalSeconds < 60;
            }
        }
    }

    public class SkillVM
    {
        public string Name { get; set; }
        public bool IsOffered { get; set; }
    }
}
