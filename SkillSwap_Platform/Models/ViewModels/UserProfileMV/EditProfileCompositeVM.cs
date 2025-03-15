using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.UserProfileMV
{
    public class EditProfileCompositeVM
    {
        public EditPersonalDetailsVM PersonalDetails { get; set; } = new EditPersonalDetailsVM();
        public EditSkillVM Skills { get; set; } = new EditSkillVM();
        public List<EducationVM> EducationEntries { get; set; } = new List<EducationVM>();
        public List<ExperienceVM> ExperienceEntries { get; set; } = new List<ExperienceVM>();
        public List<CertificateVM> CertificateEntries { get; set; } = new List<CertificateVM>();
        // Optionally, you can also include AllSkills if needed.
        public List<SkillVM> AllSkills { get; set; } = new List<SkillVM>();
    }

    public class EditPersonalDetailsVM
    {
        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid Email address.")]
        public string Email { get; set; } = string.Empty;

        public string? PersonalWebsite { get; set; }
        public string? Location { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? AboutMe { get; set; }
        [Display(Name = "Profile Photo")]
        public IFormFile? ProfileImageFile { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    public class EditSkillVM
    {
        // Summary fields stored in TblUsers.
        [Display(Name = "Offered Skills Summary")]
        public string? OfferedSkillSummary { get; set; }

        [Display(Name = "Willing Skills Summary")]
        public string? WillingSkillSummary { get; set; }

        // Detailed skills to be edited individually.
        [Display(Name = "Available Skills")]
        public List<SkillEditVM> AllSkills { get; set; } = new List<SkillEditVM>();

        public IEnumerable<SelectListItem> ProficiencyOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "1", Text = "Basic" },
            new SelectListItem { Value = "2", Text = "Intermediate" },
            new SelectListItem { Value = "3", Text = "Proficient" }
        };

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "Technical", Text = "Technical" },
            new SelectListItem { Value = "Design", Text = "Design" },
            new SelectListItem { Value = "Marketing", Text = "Marketing" },
            new SelectListItem { Value = "Data Science", Text = "Data Science" },
            new SelectListItem { Value = "Mobile", Text = "Mobile" },
            //new SelectListItem { Value = "Other", Text = "Other" }
        };
    }

    public class SkillEditVM
    {
        public int SkillId { get; set; }
        [Required(ErrorMessage = "Skill name is required.")]
        public string SkillName { get; set; } = string.Empty;
        [Display(Name = "Category")]
        public string Category { get; set; }
        [Display(Name = "Custom Category")] 
        public string? CustomCategory { get; set; }
        [Display(Name = "Proficiency Level")]
        public int? ProficiencyLevel { get; set; }
    }

    public class EducationVM
    {
        public int EducationId { get; set; }
        [Required(ErrorMessage = "Degree is required.")]
        public string Degree { get; set; } = string.Empty;
        [Required(ErrorMessage = "Degree Name is required.")]
        public string DegreeName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Institution is required.")]
        public string Institution { get; set; } = string.Empty;
        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }
        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }
        public string? Description { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ExperienceVM
    {
        public int ExperienceId { get; set; }
        [Required(ErrorMessage = "Company Name is required.")]
        public string CompanyName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Position is required.")]
        public string Position { get; set; } = string.Empty;
        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }
        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }
        public string? Description { get; set; }
        public bool IsexpDeleted { get; set; }
    }

    public class CertificateVM
    {
        public int CertificateId { get; set; }
        [Required(ErrorMessage = "Certificate Name is required.")]
        public string CertificateName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Verification Id is required.")]
        public string? VerificationId { get; set; }
        public IFormFile? CertificateFile { get; set; }
        public string? CertificateFilePath { get; set; }
        [Required(ErrorMessage = "Certificate Completion Date is required.")]
        public DateTime CertificateDate { get; set; }
        [Required(ErrorMessage = "Certificate Issuer Name is required.")]
        public string? CertificateFrom { get; set; }
        public bool IsApproved { get; set; }
        // Flag for deletion.
        public bool IscertDeleted { get; set; }
    }

    public class SkillsVM
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public int? ProficiencyLevel { get; set; }
    }
}
