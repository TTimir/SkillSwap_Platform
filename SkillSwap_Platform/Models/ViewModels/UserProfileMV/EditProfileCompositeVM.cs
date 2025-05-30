using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels.UserProfileMV
{
    public class EditProfileCompositeVM
    {
        public string Email => PersonalDetails.Email;
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
        [Required(ErrorMessage = "Share your first name!")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Add your last name!")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Set a cool username!")]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "We need a valid email!")]
        [EmailAddress(ErrorMessage = "Invalid email!")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Personal Website")]
        public string? PersonalWebsite { get; set; }
        [Required(ErrorMessage = "Tell us your location!")]
        [Display(Name = "Location")]
        public string? Location { get; set; }
        [Required(ErrorMessage = "Don't forget your address!")]
        [Display(Name = "Address")]
        public string? Address { get; set; }
        [Required(ErrorMessage = "City info, please!")]
        public string? City { get; set; }
        [Required(ErrorMessage = "Country is required.")]
        public string? Country { get; set; }
        [Display(Name = "About Me")]
        public string? AboutMe { get; set; }
        [Display(Name = "Profile Photo")]
        public IFormFile? ProfileImageFile { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    public class EditSkillVM
    {
        // Summary fields stored in TblUsers.
        [Display(Name = "Offered Skills")]
        public string? OfferedSkillSummary { get; set; }

        [Display(Name = "Want to learn about")]
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
            new SelectListItem { Value = "Graphics & Design", Text = "Graphics & Design" },
            new SelectListItem { Value = "Digital Marketing", Text = "Digital Marketing" },
            new SelectListItem { Value = "Writing & Translation", Text = "Writing & Translation" },
            new SelectListItem { Value = "Video & Animation", Text = "Video & Animation" },
            new SelectListItem { Value = "Music & Audio", Text = "Music & Audio" },
            new SelectListItem { Value = "Programming & Tech", Text = "Programming & Tech" },
            new SelectListItem { Value = "Business", Text = "Business" },
            new SelectListItem { Value = "Lifestyle", Text = "Lifestyle" },
            new SelectListItem { Value = "Trending", Text = "Trending" },
            //new SelectListItem { Value = "Other", Text = "Other" }
        };
    }

    public class SkillEditVM
    {
        public int SkillId { get; set; }
        [Required(ErrorMessage = "Skill name can't be blank!")]
        [Display(Name = "Skill Name")]
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
        [Required(ErrorMessage = "Enter your degree!")]
        [Display(Name = "Degree")]
        public string Degree { get; set; } = string.Empty;
        [Required(ErrorMessage = "Degree name is needed!")]
        [Display(Name = "Degree Name")]
        public string DegreeName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Name your institution!")]
        [Display(Name = "Institution")]
        public string Institution { get; set; } = string.Empty;
        [Required(ErrorMessage = "Start date is needed!")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }
        [Display(Name = "Description")] 
        public string? Description { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ExperienceVM
    {
        public int ExperienceId { get; set; }
        [Required(ErrorMessage = "Enter company name!")]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Enter your position!")]
        [Display(Name = "Position")]
        public string Position { get; set; } = string.Empty;
        [Required(ErrorMessage = "Select start date!")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }
        [Display(Name = "Description")]
        public string? Description { get; set; }
        public bool IsexpDeleted { get; set; }
    }

    public class CertificateVM
    {
        public int CertificateId { get; set; }
        [Required(ErrorMessage = "Name your certificate!")]
        [Display(Name = "Certificate Name")]
        public string CertificateName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Provide verification ID!")]
        [Display(Name = "Verification ID")]
        public string? VerificationId { get; set; }
        [Display(Name = "Certificate File")] 
        public IFormFile? CertificateFile { get; set; }
        public string? CertificateFilePath { get; set; }
        [Required(ErrorMessage = "Set completion date!")]
        [Display(Name = "Completion Date")]
        public DateTime? CertificateDate { get; set; }
        [Required(ErrorMessage = "Enter issuer name!")]
        [Display(Name = "Issuer Name")]
        public string? CertificateFrom { get; set; }
        public DateTime? CompleteDate { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? RejectionDate { get; set; }
        public string? RejectionReason { get; set; }

        // Flag for deletion.
        public bool IscertDeleted { get; set; }


    }

    public class SkillsVM
    {
        public int SkillId { get; set; }
        [Display(Name = "Skill Name")] 
        public string SkillName { get; set; } = string.Empty;
        [Display(Name = "Proficiency Level")]
        public int? ProficiencyLevel { get; set; }
    }
}
