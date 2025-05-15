using SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM;
using static SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM.SubmitRequestVm;

namespace SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM
{

    // Models/Verification/AdminListVm.cs
    public class AdminListVm
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public DateTime SubmittedAt { get; set; }
        public VerificationStatus Status { get; set; }
    }

    // Models/Verification/AdminDetailsVm.cs
    public class AdminDetailsVm
    {
        public long Id { get; set; }
        public string SubmittedByUser { get; set; }
        public string SubmittedByUsername { get; set; }
        public DateTime SubmittedAt { get; set; }
        public VerificationStatus Status { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string ReviewedByUsername { get; set; }
        public string ReviewComments { get; set; }
        public string GovernmentIdType { get; internal set; }
        public string GovernmentIdNumber { get; internal set; }
        public string GovernmentIdDocumentPath { get; set; }

        public IList<SkillCertificate> Certificates { get; set; } = new List<SkillCertificate>();

        public IList<EducationRecord> EducationRecords { get; set; } = new List<EducationRecord>();

        public IList<ExperienceRecord> ExperienceRecords { get; set; } = new List<ExperienceRecord>();
    }

}
