using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Skill_Swap.Models;

namespace SkillSwap_Platform.Models;

public partial class SkillSwapDbContext : DbContext
{
    public SkillSwapDbContext()
    {
    }

    public SkillSwapDbContext(DbContextOptions<SkillSwapDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<PrivacySensitiveWord> PrivacySensitiveWords { get; set; }

    public virtual DbSet<SensitiveWord> SensitiveWords { get; set; }

    public virtual DbSet<TblContract> TblContracts { get; set; }

    public virtual DbSet<TblEducation> TblEducations { get; set; }

    public virtual DbSet<TblExchange> TblExchanges { get; set; }

    public virtual DbSet<TblExchangeHistory> TblExchangeHistories { get; set; }

    public virtual DbSet<TblExperience> TblExperiences { get; set; }

    public virtual DbSet<TblKycUpload> TblKycUploads { get; set; }

    public virtual DbSet<TblLanguage> TblLanguages { get; set; }

    public virtual DbSet<TblMessage> TblMessages { get; set; }

    public virtual DbSet<TblMessageAttachment> TblMessageAttachments { get; set; }

    public virtual DbSet<TblOffer> TblOffers { get; set; }

    public virtual DbSet<TblOfferPortfolio> TblOfferPortfolios { get; set; }

    public virtual DbSet<TblReview> TblReviews { get; set; }

    public virtual DbSet<TblRole> TblRoles { get; set; }

    public virtual DbSet<TblSkill> TblSkills { get; set; }

    public virtual DbSet<TblSupportTicket> TblSupportTickets { get; set; }

    public virtual DbSet<TblUser> TblUsers { get; set; }

    public virtual DbSet<TblUserCertificate> TblUserCertificates { get; set; }

    public virtual DbSet<TblUserReport> TblUserReports { get; set; }

    public virtual DbSet<TblUserSkill> TblUserSkills { get; set; }

    public virtual DbSet<TblUserRole> TblUserRoles { get; set; }
    public virtual DbSet<TblWorkingTime> TblWorkingTimes { get; set; }

    public virtual DbSet<UserSensitiveWord> UserSensitiveWords { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=TIMIRBHINGRADIY;Database=SkillSwapDb;Trusted_Connection=True;Encrypt=false;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PrivacySensitiveWord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PrivacyS__3214EC0759BCBFB6");

            entity.Property(e => e.Word).HasMaxLength(100);
        });

        modelBuilder.Entity<SensitiveWord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Sensitiv__3214EC079C0CB70F");

            entity.Property(e => e.WarningMessage).HasMaxLength(500);
            entity.Property(e => e.Word).HasMaxLength(100);
        });

        modelBuilder.Entity<TblContract>(entity =>
        {
            entity.HasKey(e => e.ContractId).HasName("PK__TblContr__C90D3469E662177B");

            entity.Property(e => e.AssistanceRounds).HasDefaultValue(1);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.ContractUniqueId)
                .HasMaxLength(100)
                .HasDefaultValue("");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.FlowDescription).HasDefaultValue("");
            entity.Property(e => e.ModeOfLearning).HasMaxLength(50);
            entity.Property(e => e.OfferOwnerAvailability).HasMaxLength(100);
            entity.Property(e => e.OppositeExperienceLevel).HasMaxLength(50);
            entity.Property(e => e.ReceiverAcceptanceDate).HasColumnType("datetime");
            entity.Property(e => e.ReceiverAddress).HasMaxLength(255);
            entity.Property(e => e.ReceiverEmail).HasMaxLength(255);
            entity.Property(e => e.ReceiverName).HasMaxLength(100);
            entity.Property(e => e.ReceiverPlace).HasMaxLength(100);
            entity.Property(e => e.ReceiverSignature).HasMaxLength(255);
            entity.Property(e => e.ReceiverSkill).HasMaxLength(100);
            entity.Property(e => e.ReceiverUserName).HasMaxLength(100);
            entity.Property(e => e.SenderAddress).HasMaxLength(255);
            entity.Property(e => e.SenderEmail).HasMaxLength(255);
            entity.Property(e => e.SenderName).HasMaxLength(100);
            entity.Property(e => e.SenderPlace).HasMaxLength(100);
            entity.Property(e => e.SenderSignature).HasMaxLength(255);
            entity.Property(e => e.SenderSkill).HasMaxLength(100);
            entity.Property(e => e.SenderUserName).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TokenOffer).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Version).HasDefaultValue(1);

            entity.HasOne(d => d.Message).WithMany(p => p.TblContracts)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblContracts_TblMessages_MessageId");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblContracts)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblContracts_TblOffers_OfferId");

            entity.HasOne(d => d.ReceiverUser).WithMany(p => p.TblContractReceiverUsers)
                .HasForeignKey(d => d.ReceiverUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblContracts_TblUsers_ReceiverUserId");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.TblContractSenderUsers)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblContracts_TblUsers_SenderUserId");
        });

        modelBuilder.Entity<TblEducation>(entity =>
        {
            entity.HasKey(e => e.EducationId);

            entity.ToTable("tblEducation");

            entity.Property(e => e.EducationId).HasColumnName("EducationID");
            entity.Property(e => e.Degree).HasMaxLength(100);
            entity.Property(e => e.DegreeName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(100);
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.InstitutionName).HasMaxLength(200);
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.UniversityName).HasMaxLength(200);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.TblEducations)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblEducation_Users");
        });

        modelBuilder.Entity<TblExchange>(entity =>
        {
            entity.HasKey(e => e.ExchangeId);

            entity.ToTable("tblExchanges");

            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DigitalTokenExchange).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExchangeDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExchangeMode)
                .HasMaxLength(20)
                .HasDefaultValue("Online");
            entity.Property(e => e.LastStatusChangeDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.OfferId).HasColumnName("OfferID");
            entity.Property(e => e.RequesterId).HasColumnName("RequesterID");
            entity.Property(e => e.SkillIdOfferOwner).HasColumnName("SkillID_OfferOwner");
            entity.Property(e => e.SkillIdRequester).HasColumnName("SkillID_Requester");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.StatusChangeReason).HasMaxLength(500);
            entity.Property(e => e.TokensPaid).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.LastStatusChangedByNavigation).WithMany(p => p.TblExchangeLastStatusChangedByNavigations)
                .HasForeignKey(d => d.LastStatusChangedBy)
                .HasConstraintName("FK_tblExchanges_LastStatusChangedBy");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblExchanges)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblExchanges_Offers");

            entity.HasOne(d => d.Requester).WithMany(p => p.TblExchangeRequesters)
                .HasForeignKey(d => d.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblExchanges_Requester");

            entity.HasOne(d => d.SkillIdOfferOwnerNavigation).WithMany(p => p.TblExchangeSkillIdOfferOwnerNavigations)
                .HasForeignKey(d => d.SkillIdOfferOwner)
                .HasConstraintName("FK_tblExchanges_Skill_OfferOwner");

            entity.HasOne(d => d.SkillIdRequesterNavigation).WithMany(p => p.TblExchangeSkillIdRequesterNavigations)
                .HasForeignKey(d => d.SkillIdRequester)
                .HasConstraintName("FK_tblExchanges_Skill_Requester");
        });

        modelBuilder.Entity<TblExchangeHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId);

            entity.ToTable("tblExchangeHistory");

            entity.Property(e => e.HistoryId).HasColumnName("HistoryID");
            entity.Property(e => e.ChangeDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ChangedStatus).HasMaxLength(50);
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.OfferId).HasColumnName("OfferID");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.TblExchangeHistories)
                .HasForeignKey(d => d.ChangedBy)
                .HasConstraintName("FK_tblExchangeHistory_ChangedBy");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblExchangeHistories)
                .HasForeignKey(d => d.ExchangeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblExchangeHistory_Exchange");
        });

        modelBuilder.Entity<TblExperience>(entity =>
        {
            entity.HasKey(e => e.ExperienceId);

            entity.ToTable("tblExperience");

            entity.Property(e => e.ExperienceId).HasColumnName("ExperienceID");
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.Position).HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(100);
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Years).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.User).WithMany(p => p.TblExperiences)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblExperience_Users");
        });

        modelBuilder.Entity<TblKycUpload>(entity =>
        {
            entity.HasKey(e => e.KycUploadId);

            entity.ToTable("tblKycUploads");

            entity.Property(e => e.KycUploadId).HasColumnName("KycUploadID");
            entity.Property(e => e.DocumentImageUrl).HasMaxLength(500);
            entity.Property(e => e.DocumentName).HasMaxLength(200);
            entity.Property(e => e.DocumentNumber).HasMaxLength(100);
            entity.Property(e => e.UploadedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.TblKycUploads)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblKycUploads_Users");
        });

        modelBuilder.Entity<TblLanguage>(entity =>
        {
            entity.HasKey(e => e.LanguageId);

            entity.ToTable("tblLanguage");

            entity.Property(e => e.LanguageId).HasColumnName("LanguageID");
            entity.Property(e => e.Language).HasMaxLength(100);
            entity.Property(e => e.Proficiency).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.TblLanguages)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblLanguage_Users");
        });

        modelBuilder.Entity<TblMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);

            entity.ToTable("tblMessages");

            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.IsApproved).HasDefaultValue(true);
            entity.Property(e => e.MeetingLink).HasMaxLength(500);
            entity.Property(e => e.ReceiverUserId).HasColumnName("ReceiverUserID");
            entity.Property(e => e.SenderUserId).HasColumnName("SenderUserID");
            entity.Property(e => e.SentDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblMessages)
                .HasForeignKey(d => d.OfferId)
                .HasConstraintName("FK_TblMessages_Offers_OfferId");

            entity.HasOne(d => d.ReceiverUser).WithMany(p => p.TblMessageReceiverUsers)
                .HasForeignKey(d => d.ReceiverUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblMessages_Receiver");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.TblMessageSenderUsers)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblMessages_Sender");
        });

        modelBuilder.Entity<TblMessageAttachment>(entity =>
        {
            entity.HasKey(e => e.AttachmentId);

            entity.ToTable("tblMessageAttachments");

            entity.Property(e => e.AttachmentId).HasColumnName("AttachmentID");
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.UploadedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Message).WithMany(p => p.TblMessageAttachments)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblMessageAttachments_Message");
        });

        modelBuilder.Entity<TblOffer>(entity =>
        {
            entity.HasKey(e => e.OfferId);

            entity.ToTable("tblOffers");

            entity.Property(e => e.OfferId).HasColumnName("OfferID");
            entity.Property(e => e.AssistanceRounds).HasDefaultValue(1);
            entity.Property(e => e.CollaborationMethod).HasMaxLength(50);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DeletedDate).HasColumnType("datetime");
            entity.Property(e => e.DeliveryTimeDays).HasDefaultValue(3);
            entity.Property(e => e.Device)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DigitalTokenValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FreelanceType).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ProvidesSourceFiles).HasDefaultValue(false);
            entity.Property(e => e.RequiredSkillLevel).HasMaxLength(50);
            entity.Property(e => e.SkillIdOfferOwner).HasMaxLength(100);
            entity.Property(e => e.TimeCommitmentDays).HasDefaultValue(1);
            entity.Property(e => e.TokenCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Tools)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WillingSkill)
                .HasMaxLength(255)
                .HasDefaultValue("");

            entity.HasOne(d => d.User).WithMany(p => p.TblOffers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblOffers_Users");
        });

        modelBuilder.Entity<TblOfferPortfolio>(entity =>
        {
            entity.HasKey(e => e.PortfolioId).HasName("PK__tblOffer__6D3A139D4A53F9F0");

            entity.ToTable("tblOfferPortfolio");

            entity.Property(e => e.PortfolioId).HasColumnName("PortfolioID");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FileUrl).HasMaxLength(255);
            entity.Property(e => e.OfferId).HasColumnName("OfferID");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblOfferPortfolios)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OfferPortfolio_Offers");
        });

        modelBuilder.Entity<TblReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId);

            entity.ToTable("tblReviews");

            entity.Property(e => e.ReviewId).HasColumnName("ReviewID");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.RevieweeId).HasColumnName("RevieweeID");
            entity.Property(e => e.ReviewerId).HasColumnName("ReviewerID");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblReviews)
                .HasForeignKey(d => d.ExchangeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviews_Exchanges");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblReviews)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblReview_Offer");

            entity.HasOne(d => d.Reviewee).WithMany(p => p.TblReviewReviewees)
                .HasForeignKey(d => d.RevieweeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviews_Reviewee");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.TblReviewReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviews_Reviewer");

            entity.HasOne(d => d.User).WithMany(p => p.TblReviewUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblReview_User");
        });

        modelBuilder.Entity<TblRole>(entity =>
        {
            entity.HasKey(e => e.RoleId);

            entity.ToTable("tblRoles");

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<TblSkill>(entity =>
        {
            entity.HasKey(e => e.SkillId);

            entity.ToTable("tblSkills");

            entity.Property(e => e.SkillId).HasColumnName("SkillID");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SkillCategory).HasMaxLength(100);
            entity.Property(e => e.SkillName).HasMaxLength(100);
        });

        modelBuilder.Entity<TblSupportTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId);

            entity.ToTable("tblSupportTickets");

            entity.Property(e => e.TicketId).HasColumnName("TicketID");
            entity.Property(e => e.ClosedDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Open");
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblSupportTickets)
                .HasForeignKey(d => d.ExchangeId)
                .HasConstraintName("FK_tblSupportTickets_Exchanges");

            entity.HasOne(d => d.User).WithMany(p => p.TblSupportTickets)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblSupportTickets_Users");
        });

        modelBuilder.Entity<TblUser>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("tblUsers");

            entity.HasIndex(e => e.Email, "IX_tblUsers_Email").IsUnique();

            entity.HasIndex(e => e.UserName, "IX_tblUsers_UserName").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.ContactNo).HasMaxLength(20);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CurrentLocation).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Designation).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FailedOtpAttempts).HasDefaultValue(0);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Languages).HasMaxLength(200);
            entity.Property(e => e.LastActive).HasColumnType("datetime");
            entity.Property(e => e.LastFailedOtpAttempt).HasColumnType("datetime");
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.LockoutEndTime).HasColumnType("datetime");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash).HasMaxLength(200);
            entity.Property(e => e.PersonalWebsite).HasMaxLength(200);
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("User");
            entity.Property(e => e.Salt).HasMaxLength(200);
            entity.Property(e => e.TotpSecret).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.Zip).HasMaxLength(20);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "TblUserRole",
                    r => r.HasOne<TblRole>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_tblUserRoles_Roles"),
                    l => l.HasOne<TblUser>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_tblUserRoles_Users"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("tblUserRoles");
                        j.IndexerProperty<int>("UserId").HasColumnName("UserID");
                        j.IndexerProperty<int>("RoleId").HasColumnName("RoleID");
                    });
        });

        modelBuilder.Entity<TblUserCertificate>(entity =>
        {
            entity.HasKey(e => e.CertificateId);

            entity.ToTable("tblUserCertificates");

            entity.Property(e => e.CertificateId).HasColumnName("CertificateID");
            entity.Property(e => e.ApprovedByAdminId).HasColumnName("ApprovedByAdminID");
            entity.Property(e => e.ApprovedDate).HasColumnType("datetime");
            entity.Property(e => e.CertificateFilePath).HasMaxLength(500);
            entity.Property(e => e.CertificateFrom).HasMaxLength(100);
            entity.Property(e => e.CertificateName).HasMaxLength(200);
            entity.Property(e => e.CompleteDate).HasColumnType("datetime");
            entity.Property(e => e.SkillId).HasColumnName("SkillID");
            entity.Property(e => e.SubmittedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.VerificationId)
                .HasMaxLength(100)
                .HasDefaultValue("");

            entity.HasOne(d => d.ApprovedByAdmin).WithMany(p => p.TblUserCertificateApprovedByAdmins)
                .HasForeignKey(d => d.ApprovedByAdminId)
                .HasConstraintName("FK_tblUserCertificates_ApprovedBy");

            entity.HasOne(d => d.Skill).WithMany(p => p.TblUserCertificates)
                .HasForeignKey(d => d.SkillId)
                .HasConstraintName("FK_tblUserCertificates_Skills");

            entity.HasOne(d => d.User).WithMany(p => p.TblUserCertificateUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserCertificates_Users");
        });

        modelBuilder.Entity<TblUserReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            entity.ToTable("tblUserReports");

            entity.Property(e => e.ReportId).HasColumnName("ReportID");
            entity.Property(e => e.ActionTaken).HasMaxLength(100);
            entity.Property(e => e.ReportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ReportedUserId).HasColumnName("ReportedUserID");
            entity.Property(e => e.ReporterUserId).HasColumnName("ReporterUserID");
            entity.Property(e => e.ReviewedDate).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.ReportedUser).WithMany(p => p.TblUserReportReportedUsers)
                .HasForeignKey(d => d.ReportedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserReports_ReportedUser");

            entity.HasOne(d => d.ReporterUser).WithMany(p => p.TblUserReportReporterUsers)
                .HasForeignKey(d => d.ReporterUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserReports_Reporter");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.TblUserReportReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_tblUserReports_ReviewedBy");
        });

        modelBuilder.Entity<TblUserSkill>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.SkillId });

            entity.ToTable("tblUserSkills");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.SkillId).HasColumnName("SkillID");

            entity.HasOne(d => d.Skill).WithMany(p => p.TblUserSkills)
                .HasForeignKey(d => d.SkillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserSkills_Skills");

            entity.HasOne(d => d.User).WithMany(p => p.TblUserSkills)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserSkills_Users");
        });

        modelBuilder.Entity<TblWorkingTime>(entity =>
        {
            entity.HasKey(e => e.WorkingTimeId);

            entity.ToTable("tblWorkingTime");

            entity.Property(e => e.WorkingTimeId).HasColumnName("WorkingTimeID");
            entity.Property(e => e.ScheduleType).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.TblWorkingTimes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblWorkingTime_Users");
        });

        modelBuilder.Entity<UserSensitiveWord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserSens__3214EC074AE3ACA8");

            entity.Property(e => e.DetectedOn)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Message).WithMany(p => p.UserSensitiveWords)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSensitiveWords_Message");

            entity.HasOne(d => d.SensitiveWord).WithMany(p => p.UserSensitiveWords)
                .HasForeignKey(d => d.SensitiveWordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSensitiveWords_SensitiveWord");

            entity.HasOne(d => d.User).WithMany(p => p.UserSensitiveWords)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSensitiveWords_User");
        });

        //modelBuilder.Entity<TblUserRole>(entity =>
        //{
        //    entity.ToTable("tblUserRoles");

        //    // Set composite primary key
        //    entity.HasKey(e => new { e.UserId, e.RoleId });

        //    // Configure foreign key to tblUsers
        //    entity.HasOne(e => e.User)
        //          .WithMany(u => u.TblUserRoles) // You'll need to add this navigation property to TblUser if desired
        //          .HasForeignKey(e => e.UserId)
        //          .OnDelete(DeleteBehavior.ClientSetNull)
        //          .HasConstraintName("FK_tblUserRoles_Users");

        //    // Configure foreign key to tblRoles
        //    entity.HasOne(e => e.Role)
        //          .WithMany(r => r.TblUserRoles) // You'll need to add this navigation property to TblRole if desired
        //          .HasForeignKey(e => e.RoleId)
        //          .OnDelete(DeleteBehavior.ClientSetNull)
        //          .HasConstraintName("FK_tblUserRoles_Roles");
        //});

        modelBuilder.Entity<TblUserRole>()
            .HasKey(t => new { t.UserId, t.RoleId });
        //modelBuilder.Entity<TblUserRole>().HasNoKey();

        modelBuilder.Entity<TblUser>()
       .HasIndex(u => u.UserName)
       .IsUnique();

        modelBuilder.Entity<TblUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
