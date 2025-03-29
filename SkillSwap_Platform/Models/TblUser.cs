using Skill_Swap.Models;
using System;
using System.Collections.Generic;

namespace SkillSwap_Platform.Models;

public partial class TblUser
{
    public int UserId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string? Designation { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Salt { get; set; } = null!;

    public string? Email { get; set; }

    public string ContactNo { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsVerified { get; set; }

    public bool IsActive { get; set; }

    public bool IsHeld { get; set; }

    public string? Description { get; set; }

    public string? Education { get; set; }

    public string? Experience { get; set; }

    public string? Languages { get; set; }

    public string? SocialMediaLinks { get; set; }

    public string? PersonalWebsite { get; set; }

    public string? CurrentLocation { get; set; }

    public string? WorkingTime { get; set; }

    public string? ProfileImageUrl { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? Zip { get; set; }

    public string? DesiredSkillAreas { get; set; }

    public string? OfferedSkillAreas { get; set; }

    public string? AboutMe { get; set; }

    public DateTime? LastActive { get; set; }

    public int? FailedOtpAttempts { get; set; }

    public DateTime? LastFailedOtpAttempt { get; set; }

    public DateTime? LockoutEndTime { get; set; }

    public string Role { get; set; } = null!;

    public string? TotpSecret { get; set; }

    public bool IsOnboardingCompleted { get; set; }

    public double? JobSuccessRate { get; set; }

    public double? RecommendedPercentage { get; set; }

    public virtual ICollection<TblContract> TblContractReceiverUsers { get; set; } = new List<TblContract>();

    public virtual ICollection<TblContract> TblContractSenderUsers { get; set; } = new List<TblContract>();

    public virtual ICollection<TblEducation> TblEducations { get; set; } = new List<TblEducation>();

    public virtual ICollection<TblExchangeHistory> TblExchangeHistories { get; set; } = new List<TblExchangeHistory>();

    public virtual ICollection<TblExchange> TblExchangeLastStatusChangedByNavigations { get; set; } = new List<TblExchange>();

    public virtual ICollection<TblExchange> TblExchangeRequesters { get; set; } = new List<TblExchange>();

    public virtual ICollection<TblExperience> TblExperiences { get; set; } = new List<TblExperience>();

    public virtual ICollection<TblKycUpload> TblKycUploads { get; set; } = new List<TblKycUpload>();

    public virtual ICollection<TblLanguage> TblLanguages { get; set; } = new List<TblLanguage>();

    public virtual ICollection<TblMessage> TblMessageReceiverUsers { get; set; } = new List<TblMessage>();

    public virtual ICollection<TblMessage> TblMessageSenderUsers { get; set; } = new List<TblMessage>();

    public virtual ICollection<TblOffer> TblOffers { get; set; } = new List<TblOffer>();

    public virtual ICollection<TblReview> TblReviewReviewees { get; set; } = new List<TblReview>();

    public virtual ICollection<TblReview> TblReviewReviewers { get; set; } = new List<TblReview>();

    public virtual ICollection<TblReview> TblReviewUsers { get; set; } = new List<TblReview>();

    public virtual ICollection<TblSupportTicket> TblSupportTickets { get; set; } = new List<TblSupportTicket>();

    public virtual ICollection<TblUserCertificate> TblUserCertificateApprovedByAdmins { get; set; } = new List<TblUserCertificate>();

    public virtual ICollection<TblUserCertificate> TblUserCertificateUsers { get; set; } = new List<TblUserCertificate>();

    public virtual ICollection<TblUserReport> TblUserReportReportedUsers { get; set; } = new List<TblUserReport>();

    public virtual ICollection<TblUserReport> TblUserReportReporterUsers { get; set; } = new List<TblUserReport>();

    public virtual ICollection<TblUserReport> TblUserReportReviewedByNavigations { get; set; } = new List<TblUserReport>();

    public virtual ICollection<TblUserSkill> TblUserSkills { get; set; } = new List<TblUserSkill>();

    public virtual ICollection<TblWorkingTime> TblWorkingTimes { get; set; } = new List<TblWorkingTime>();

    public virtual ICollection<UserSensitiveWord> UserSensitiveWords { get; set; } = new List<UserSensitiveWord>();

    public virtual ICollection<TblRole> Roles { get; set; } = new List<TblRole>();
    public virtual ICollection<TblUserRole> TblUserRoles { get; set; } = new HashSet<TblUserRole>();
}
