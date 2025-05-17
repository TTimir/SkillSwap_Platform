using Microsoft.AspNetCore.Mvc.ModelBinding;
using SkillSwap_Platform.Models.ViewModels.UserProfileMV;
using System.ComponentModel.DataAnnotations;
using static SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM.SubmitRequestVm;

namespace SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM
{
    public class SubmitRequestVm
    {
        [Required] public string GovernmentIdType { get; set; }
        [Required] public string GovernmentIdNumber { get; set; }
        [Required] public IFormFile GovernmentIdDocument { get; set; }

        [Required]
        public IList<SkillCertificate> Certificates { get; set; } = new List<SkillCertificate>();

        [Required]
        public IList<EducationRecord> EducationRecords { get; set; } = new List<EducationRecord>();

        public IList<ExperienceRecord> ExperienceRecords { get; set; } = new List<ExperienceRecord>();

        public class SkillCertificate
        {
            [Required] public string SkillName { get; set; }
            [Required] public IFormFile CertificateFile { get; set; }
        }

        public class EducationRecord
        {
            [Required] public string Degree { get; set; }
            [Required] public string Institution { get; set; }
            [Required] public IFormFile EduProofFile { get; set; }
        }

        public class ExperienceRecord
        {
            public string Company { get; set; }
            public string Role { get; set; }
            public IFormFile? ExpProofFile { get; set; }
        }
    }

    public enum VerificationStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

}
