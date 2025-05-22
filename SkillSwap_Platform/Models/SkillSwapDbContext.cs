using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Skill_Swap.Models;
using SkillSwap_Platform.Models.ViewModels;

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

    public virtual DbSet<AdminNotification> AdminNotifications { get; set; }

    public virtual DbSet<CancellationRequest> CancellationRequests { get; set; }

    public virtual DbSet<MiningLog> MiningLogs { get; set; }

    public virtual DbSet<NewsletterLog> NewsletterLogs { get; set; }

    public virtual DbSet<NewsletterTemplate> NewsletterTemplates { get; set; }

    public virtual DbSet<OtpAttempt> OtpAttempts { get; set; }

    public virtual DbSet<PaymentLog> PaymentLogs { get; set; }

    public virtual DbSet<PrivacySensitiveWord> PrivacySensitiveWords { get; set; }

    public virtual DbSet<SensitiveWord> SensitiveWords { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<TblBadge> TblBadges { get; set; }

    public virtual DbSet<TblBadgeAward> TblBadgeAwards { get; set; }

    public virtual DbSet<TblBlogPost> TblBlogPosts { get; set; }

    public virtual DbSet<TblContract> TblContracts { get; set; }

    public virtual DbSet<TblEducation> TblEducations { get; set; }

    public virtual DbSet<TblEscrow> TblEscrows { get; set; }

    public virtual DbSet<TblExchange> TblExchanges { get; set; }

    public virtual DbSet<TblExchangeHistory> TblExchangeHistories { get; set; }

    public virtual DbSet<TblExchangeSkill> TblExchangeSkills { get; set; }

    public virtual DbSet<TblExperience> TblExperiences { get; set; }

    public virtual DbSet<TblInPersonMeeting> TblInPersonMeetings { get; set; }

    public virtual DbSet<TblInpersonMeetingProof> TblInpersonMeetingProofs { get; set; }

    public virtual DbSet<TblKycUpload> TblKycUploads { get; set; }

    public virtual DbSet<TblLanguage> TblLanguages { get; set; }

    public virtual DbSet<TblMeeting> TblMeetings { get; set; }

    public virtual DbSet<TblMessage> TblMessages { get; set; }

    public virtual DbSet<TblMessageAttachment> TblMessageAttachments { get; set; }

    public virtual DbSet<TblNewsletterSubscriber> TblNewsletterSubscribers { get; set; }

    public virtual DbSet<TblNotification> TblNotifications { get; set; }

    public virtual DbSet<TblOffer> TblOffers { get; set; }

    public virtual DbSet<TblOfferFaq> TblOfferFaqs { get; set; }

    public virtual DbSet<TblOfferFlag> TblOfferFlags { get; set; }

    public virtual DbSet<TblOfferPortfolio> TblOfferPortfolios { get; set; }

    public virtual DbSet<TblOnboardingProgress> TblOnboardingProgresses { get; set; }

    public virtual DbSet<TblPasswordResetToken> TblPasswordResetTokens { get; set; }

    public virtual DbSet<TblResource> TblResources { get; set; }

    public virtual DbSet<TblReview> TblReviews { get; set; }

    public virtual DbSet<TblReviewModerationHistory> TblReviewModerationHistories { get; set; }

    public virtual DbSet<TblReviewReply> TblReviewReplies { get; set; }

    public virtual DbSet<TblRole> TblRoles { get; set; }

    public virtual DbSet<TblSkill> TblSkills { get; set; }

    public virtual DbSet<TblSkillSwapFaq> TblSkillSwapFaqs { get; set; }

    public virtual DbSet<TblSupportTicket> TblSupportTickets { get; set; }

    public virtual DbSet<TblTokenTransaction> TblTokenTransactions { get; set; }

    public virtual DbSet<TblUser> TblUsers { get; set; }

    public virtual DbSet<TblUserCertificate> TblUserCertificates { get; set; }

    public virtual DbSet<TblUserContactRequest> TblUserContactRequests { get; set; }

    public virtual DbSet<TblUserFlag> TblUserFlags { get; set; }

    public virtual DbSet<TblUserHoldHistory> TblUserHoldHistories { get; set; }

    public virtual DbSet<TblUserReport> TblUserReports { get; set; }

    public virtual DbSet<TblUserSkill> TblUserSkills { get; set; }

    public virtual DbSet<TblUserWishlist> TblUserWishlists { get; set; }
    public virtual DbSet<TblUserRole> TblUserRoles { get; set; }

    public virtual DbSet<TblWorkingTime> TblWorkingTimes { get; set; }

    public virtual DbSet<TokenEmissionSetting> TokenEmissionSettings { get; set; }

    public virtual DbSet<UserGoogleToken> UserGoogleTokens { get; set; }

    public virtual DbSet<UserMiningProgress> UserMiningProgresses { get; set; }

    public virtual DbSet<UserSensitiveWord> UserSensitiveWords { get; set; }

    public virtual DbSet<VerificationRequest> VerificationRequests { get; set; }
    public virtual DbSet<ReviewAggregate> ReviewAggregates { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=TIMIRBHINGRADIY;Database=SkillSwapDb;Trusted_Connection=True;Encrypt=false;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminNotification>(entity =>
        {
            entity.ToTable("AdminNotification");

            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Subject).HasMaxLength(256);
            entity.Property(e => e.ToEmail).HasMaxLength(256);
        });

        modelBuilder.Entity<CancellationRequest>(entity =>
        {
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Subscription).WithMany(p => p.CancellationRequests)
                .HasForeignKey(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CancellationRequests_Subscriptions");
        });

        modelBuilder.Entity<MiningLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MiningLo__3214EC079A9B841C");

            entity.ToTable("MiningLog");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 4)");

            entity.HasOne(d => d.User).WithMany(p => p.MiningLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MiningLog_User");
        });

        modelBuilder.Entity<NewsletterLog>(entity =>
        {
            entity.ToTable("NewsletterLog");

            entity.Property(e => e.RecipientEmail)
                .HasMaxLength(255)
                .HasDefaultValue("");
            entity.Property(e => e.SentAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SentByAdmin)
                .HasMaxLength(450)
                .HasDefaultValue("");
            entity.Property(e => e.Subject)
                .HasMaxLength(255)
                .HasDefaultValue("");
        });

        modelBuilder.Entity<NewsletterTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId);

            entity.ToTable("NewsletterTemplate");

            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasDefaultValue("");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasDefaultValue("");
        });

        modelBuilder.Entity<OtpAttempt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OtpAttem__3214EC077F718846");

            entity.HasIndex(e => e.AttemptedAt, "IX_OtpAttempts_AttemptedAt");

            entity.HasIndex(e => e.UserId, "IX_OtpAttempts_UserId");

            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Method).HasMaxLength(20);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        modelBuilder.Entity<PaymentLog>(entity =>
        {
            entity.Property(e => e.OrderId).HasMaxLength(128);
            entity.Property(e => e.PaymentId).HasMaxLength(128);
            entity.Property(e => e.ProcessedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

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

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Subscrip__3214EC076342599A");

            entity.Property(e => e.BillingCycle)
                .HasMaxLength(10)
                .HasDefaultValue("monthly");
            entity.Property(e => e.GatewayOrderId).HasMaxLength(200);
            entity.Property(e => e.GatewayPaymentId).HasMaxLength(200);
            entity.Property(e => e.IsAutoRenew).HasDefaultValue(true);
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PlanName).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Subscriptions_Users");
        });

        modelBuilder.Entity<TblBadge>(entity =>
        {
            entity.HasKey(e => e.BadgeId).HasName("PK__TblBadge__1918235C1482CF0D");

            entity.Property(e => e.BadgeId).ValueGeneratedNever();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.IconUrl).HasMaxLength(512);
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Tier)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblBadgeAward>(entity =>
        {
            entity.HasKey(e => e.AwardId).HasName("PK__TblBadge__B08935FEC0617756");

            entity.Property(e => e.AwardedAt)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Badge).WithMany(p => p.TblBadgeAwards)
                .HasForeignKey(d => d.BadgeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TblBadgeA__Badge__68A8708A");
        });

        modelBuilder.Entity<TblBlogPost>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tblBlogP__3214EC073374D83A");

            entity.ToTable("tblBlogPosts");

            entity.Property(e => e.CoverImagePath).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Summary).HasMaxLength(2000);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<TblContract>(entity =>
        {
            entity.HasKey(e => e.ContractId).HasName("PK__TblContr__C90D3469E662177B");

            entity.Property(e => e.AcceptedDate).HasColumnType("datetime");
            entity.Property(e => e.AssistanceRounds).HasDefaultValue(1);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.ContractUniqueId)
                .HasMaxLength(100)
                .HasDefaultValue("");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.DeclinedDate).HasColumnType("datetime");
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
            entity.Property(e => e.RequestDate).HasColumnType("datetime");
            entity.Property(e => e.ResponseDate).HasColumnType("datetime");
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

        modelBuilder.Entity<TblEscrow>(entity =>
        {
            entity.HasKey(e => e.EscrowId).HasName("PK__tblEscro__55766534022E078F");

            entity.ToTable("tblEscrows");

            entity.Property(e => e.EscrowId).HasColumnName("EscrowID");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BuyerId).HasColumnName("BuyerID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.HandledByAdminId).HasColumnName("HandledByAdminID");
            entity.Property(e => e.SellerId).HasColumnName("SellerID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Buyer).WithMany(p => p.TblEscrowBuyers)
                .HasForeignKey(d => d.BuyerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Escrow_Buyer");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblEscrows)
                .HasForeignKey(d => d.ExchangeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Escrow_Exchange");

            entity.HasOne(d => d.HandledByAdmin).WithMany(p => p.TblEscrowHandledByAdmins)
                .HasForeignKey(d => d.HandledByAdminId)
                .HasConstraintName("FK_Escrow_Admin");

            entity.HasOne(d => d.Seller).WithMany(p => p.TblEscrowSellers)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Escrow_Seller");
        });

        modelBuilder.Entity<TblExchange>(entity =>
        {
            entity.HasKey(e => e.ExchangeId);

            entity.ToTable("tblExchanges");

            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.CompletionDate).HasColumnType("datetime");
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
            entity.Property(e => e.RequestDate).HasColumnType("datetime");
            entity.Property(e => e.ResponseDate).HasColumnType("datetime");
            entity.Property(e => e.SkillIdOfferOwner).HasColumnName("SkillID_OfferOwner");
            entity.Property(e => e.SkillIdRequester).HasColumnName("SkillID_Requester");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.StatusChangeReason).HasMaxLength(500);
            entity.Property(e => e.ThisMeetingLink).HasColumnName("thisMeetingLink");
            entity.Property(e => e.TokensPaid).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Contract).WithMany(p => p.TblExchanges)
                .HasForeignKey(d => d.ContractId)
                .HasConstraintName("FK_TblExchanges_TblContracts");

            entity.HasOne(d => d.LastStatusChangedByNavigation).WithMany(p => p.TblExchanges)
                .HasForeignKey(d => d.LastStatusChangedBy)
                .HasConstraintName("FK_tblExchanges_LastStatusChangedBy");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblExchanges)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblExchanges_Offers");

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
            entity.Property(e => e.ActionType).HasMaxLength(50);
            entity.Property(e => e.ChangeDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ChangedStatus).HasMaxLength(50);
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.MeetingVerifiedDate).HasColumnType("datetime");
            entity.Property(e => e.OfferId).HasColumnName("OfferID");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.VerificationNote).HasMaxLength(500);

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.TblExchangeHistories)
                .HasForeignKey(d => d.ChangedBy)
                .HasConstraintName("FK_tblExchangeHistory_ChangedBy");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblExchangeHistories)
                .HasForeignKey(d => d.ExchangeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblExchangeHistory_Exchange");
        });

        modelBuilder.Entity<TblExchangeSkill>(entity =>
        {
            entity.HasKey(e => e.ExchangeSkillId).HasName("PK__tblExcha__1DA7221AAE6F3C7B");

            entity.ToTable("tblExchangeSkills");

            entity.Property(e => e.Role).HasMaxLength(50);
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

        modelBuilder.Entity<TblInPersonMeeting>(entity =>
        {
            entity.HasKey(e => e.InPersonMeetingId).HasName("PK__TblInPer__32D8C5478EC8E95C");

            entity.HasIndex(e => new { e.ExchangeId, e.CreatedDate }, "IX_TblInPersonMeetings_ExchangeId_CreatedDate").IsDescending(false, true);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.InpersonMeetingOtpOfferOwner).HasMaxLength(50);
            entity.Property(e => e.InpersonMeetingOtpOtherParty).HasMaxLength(50);
            entity.Property(e => e.InpersonMeetingVerifiedDate).HasColumnType("datetime");
            entity.Property(e => e.InpersonMeetingVerifiedDateOfferOwner).HasColumnType("datetime");
            entity.Property(e => e.InpersonMeetingVerifiedDateOtherParty).HasColumnType("datetime");
            entity.Property(e => e.MeetingLocation).HasMaxLength(255);
            entity.Property(e => e.MeetingScheduledDateTime).HasColumnType("datetime");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblInPersonMeetings)
                .HasForeignKey(d => d.ExchangeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblInPersonMeeting_Exchange");
        });

        modelBuilder.Entity<TblInpersonMeetingProof>(entity =>
        {
            entity.HasKey(e => e.ProofId).HasName("PK__TblInper__E33C700C8A22B7C5");

            entity.ToTable("TblInpersonMeetingProof");

            entity.Property(e => e.CapturedByUsername).HasMaxLength(255);
            entity.Property(e => e.EndProofDateTime).HasColumnType("datetime");
            entity.Property(e => e.EndProofImageUrl).HasMaxLength(255);
            entity.Property(e => e.EndProofLocation).HasMaxLength(255);
            entity.Property(e => e.StartProofDateTime).HasColumnType("datetime");
            entity.Property(e => e.StartProofImageUrl)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.StartProofLocation)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.UploadedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
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

        modelBuilder.Entity<TblMeeting>(entity =>
        {
            entity.HasKey(e => e.MeetingId).HasName("PK__TblMeeti__E9F9E94CE58BA0C1");

            entity.ToTable("tblMeetings");

            entity.Property(e => e.ActualEndTime).HasColumnType("datetime");
            entity.Property(e => e.ActualStartTime).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.MeetingEndTime).HasColumnType("datetime");
            entity.Property(e => e.MeetingLink).HasMaxLength(255);
            entity.Property(e => e.MeetingStartTime).HasColumnType("datetime");
            entity.Property(e => e.MeetingType).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Scheduled");
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<TblMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);

            entity.ToTable("tblMessages");

            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.IsApproved).HasDefaultValue(true);
            entity.Property(e => e.MeetingLink).HasMaxLength(500);
            entity.Property(e => e.MessageType)
                .HasMaxLength(50)
                .HasDefaultValue("Normal");
            entity.Property(e => e.ReceiverUserId).HasColumnName("ReceiverUserID");
            entity.Property(e => e.SenderUserId).HasColumnName("SenderUserID");
            entity.Property(e => e.SentDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.ApprovedByAdmin).WithMany(p => p.TblMessageApprovedByAdmins)
                .HasForeignKey(d => d.ApprovedByAdminId)
                .HasConstraintName("FK_tblMessages_ApprovedByAdmin");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblMessages)
                .HasForeignKey(d => d.OfferId)
                .HasConstraintName("FK_tblMessages_Offers_OfferId");

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

        modelBuilder.Entity<TblNewsletterSubscriber>(entity =>
        {
            entity.HasKey(e => e.NewsletterSubscriberId).HasName("PK__tblNewsl__73915D707F301EE9");

            entity.ToTable("tblNewsletterSubscriber");

            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.SubscribedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<TblNotification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK_Notifications");

            entity.ToTable("tblNotifications");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Url).HasMaxLength(500);
        });

        modelBuilder.Entity<TblOffer>(entity =>
        {
            entity.HasKey(e => e.OfferId);

            entity.ToTable("tblOffers");

            entity.Property(e => e.OfferId).HasColumnName("OfferID");
            entity.Property(e => e.Address).HasMaxLength(255);
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

        modelBuilder.Entity<TblOfferFaq>(entity =>
        {
            entity.HasKey(e => e.FaqId).HasName("PK__tblOffer__9C741C43E70DD7FA");

            entity.ToTable("tblOfferFaq");

            entity.Property(e => e.Answer).HasMaxLength(1000);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Question).HasMaxLength(200);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblOfferFaqs)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblOfferFaq_TblOffer");
        });

        modelBuilder.Entity<TblOfferFlag>(entity =>
        {
            entity.HasKey(e => e.OfferFlagId);

            entity.ToTable("TblOfferFlag");

            entity.HasIndex(e => e.OfferId, "IX_TblOfferFlag_OfferId");

            entity.Property(e => e.AdminAction).HasMaxLength(50);
            entity.Property(e => e.AdminActionDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FlaggedDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.AdminUser).WithMany(p => p.TblOfferFlagAdminUsers)
                .HasForeignKey(d => d.AdminUserId)
                .HasConstraintName("FK_TblOfferFlag_AdminUser");

            entity.HasOne(d => d.FlaggedByUser).WithMany(p => p.TblOfferFlagFlaggedByUsers)
                .HasForeignKey(d => d.FlaggedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblOfferFlag_Users");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblOfferFlags)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblOfferFlag_TblOffer");
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

        modelBuilder.Entity<TblOnboardingProgress>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK_TblOnboardingProgress_UserId");

            entity.ToTable("TblOnboardingProgress");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.TotalTokensGiven).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<TblPasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TblPassw__3214EC07A7EA8FDC");

            entity.HasIndex(e => e.Token, "IX_TblPasswordResetTokens_Token").IsUnique();

            entity.Property(e => e.Token).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.TblPasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblPasswordResetTokens_TblUsers");
        });

        modelBuilder.Entity<TblResource>(entity =>
        {
            entity.HasKey(e => e.ResourceId).HasName("PK__tblResou__4ED1816F2249D411");

            entity.ToTable("tblResources");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ResourceType).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(200);
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
            entity.Property(e => e.FlaggedDate).HasColumnType("datetime");
            entity.Property(e => e.RevieweeId).HasColumnName("RevieweeID");
            entity.Property(e => e.ReviewerEmail).HasMaxLength(255);
            entity.Property(e => e.ReviewerId).HasColumnName("ReviewerID");
            entity.Property(e => e.ReviewerName).HasMaxLength(255);

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblReviews)
                .HasForeignKey(d => d.ExchangeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviews_Exchanges");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblReviews)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviews_Offer");

            entity.HasOne(d => d.Reviewee).WithMany(p => p.TblReviewReviewees)
                .HasForeignKey(d => d.RevieweeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviews_Reviewee");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.TblReviewReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviews_Reviewer");
        });

        modelBuilder.Entity<TblReviewModerationHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__tblRevie__4D7B4ABDC4451958");

            entity.ToTable("tblReviewModerationHistory");

            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Admin).WithMany(p => p.TblReviewModerationHistories)
                .HasForeignKey(d => d.AdminId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblReviewModerationHistory_Admin");

            entity.HasOne(d => d.Reply).WithMany(p => p.TblReviewModerationHistories)
                .HasForeignKey(d => d.ReplyId)
                .HasConstraintName("FK_tblReviewModerationHistories_Reply");

            entity.HasOne(d => d.Review).WithMany(p => p.TblReviewModerationHistories)
                .HasForeignKey(d => d.ReviewId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReviewHist_Review");
        });

        modelBuilder.Entity<TblReviewReply>(entity =>
        {
            entity.HasKey(e => e.ReplyId);

            entity.ToTable("tblReviewReply");

            entity.Property(e => e.Comments).HasMaxLength(1000);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DeletionReason).HasMaxLength(500);
            entity.Property(e => e.FlaggedDate).HasColumnType("datetime");

            entity.HasOne(d => d.ReplierUser).WithMany(p => p.TblReviewReplies)
                .HasForeignKey(d => d.ReplierUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviewReply_ReplierUser");

            entity.HasOne(d => d.Review).WithMany(p => p.TblReviewReplies)
                .HasForeignKey(d => d.ReviewId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblReviewReply_Review");
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

        modelBuilder.Entity<TblSkillSwapFaq>(entity =>
        {
            entity.HasKey(e => e.FaqId).HasName("PK__tblSkill__9C741C43DCE41AF8");

            entity.ToTable("tblSkillSwapFaq");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Section).HasMaxLength(100);
        });

        modelBuilder.Entity<TblSupportTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId);

            entity.ToTable("tblSupportTickets");

            entity.Property(e => e.TicketId).HasColumnName("TicketID");
            entity.Property(e => e.AssignedAdminId).HasColumnName("AssignedAdminID");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.ResolvedDate).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Open");
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.AssignedAdmin).WithMany(p => p.TblSupportTicketAssignedAdmins)
                .HasForeignKey(d => d.AssignedAdminId)
                .HasConstraintName("FK_SupportTickets_Admin");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblSupportTickets)
                .HasForeignKey(d => d.ExchangeId)
                .HasConstraintName("FK_tblSupportTickets_Exchanges");

            entity.HasOne(d => d.User).WithMany(p => p.TblSupportTicketUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblSupportTickets_Users");
        });

        modelBuilder.Entity<TblTokenTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__TblToken__55433A6B698057C6");

            entity.Property(e => e.AdminAdjustmentReason).HasMaxLength(256);
            entity.Property(e => e.AdminAdjustmentType).HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 6)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.NewBalance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldBalance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxType)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblTokenTransactions)
                .HasForeignKey(d => d.ExchangeId)
                .HasConstraintName("FK_TokTrans_Exchange");

            entity.HasOne(d => d.FromUser).WithMany(p => p.TblTokenTransactionFromUsers)
                .HasForeignKey(d => d.FromUserId)
                .HasConstraintName("FK_TokTrans_FromUser");

            entity.HasOne(d => d.ToUser).WithMany(p => p.TblTokenTransactionToUsers)
                .HasForeignKey(d => d.ToUserId)
                .HasConstraintName("FK_TokTrans_ToUser");
        });

        modelBuilder.Entity<TblUser>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("tblUsers");

            entity.HasIndex(e => e.Email, "IX_tblUsers_Email").IsUnique();

            entity.HasIndex(e => e.IsEscrowAccount, "IX_tblUsers_IsEscrowAccount")
                .IsUnique()
                .HasFilter("([IsEscrowAccount]=(1))");

            entity.HasIndex(e => e.UserName, "IX_tblUsers_UserName").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.AverageRating)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.ContactNo).HasMaxLength(20);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CurrentLocation).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Designation).HasMaxLength(50);
            entity.Property(e => e.DigitalTokenBalance).HasColumnType("decimal(18, 8)");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.EmailChangeExpires).HasColumnType("datetime");
            entity.Property(e => e.EmailChangeOtp)
                .HasMaxLength(6)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.FailedOtpAttempts).HasDefaultValue(0);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.HeldAt).HasColumnType("datetime");
            entity.Property(e => e.HeldUntil).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Languages).HasMaxLength(200);
            entity.Property(e => e.LastActive).HasColumnType("datetime");
            entity.Property(e => e.LastFailedOtpAttempt).HasColumnType("datetime");
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.LockoutEndTime).HasColumnType("datetime");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash).HasMaxLength(200);
            entity.Property(e => e.PendingEmail).HasMaxLength(256);
            entity.Property(e => e.PersonalWebsite).HasMaxLength(200);
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
            entity.Property(e => e.ReleaseReason).HasMaxLength(500);
            entity.Property(e => e.ReleasedAt).HasColumnType("datetime");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("User");
            entity.Property(e => e.Salt).HasMaxLength(200);
            entity.Property(e => e.SecurityStamp).HasDefaultValueSql("(newid())");
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
            entity.Property(e => e.RejectDate).HasColumnType("datetime");
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

        modelBuilder.Entity<TblUserContactRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_SupportRequests");

            entity.ToTable("tblUserContactRequests");

            entity.Property(e => e.AttachmentContentType).HasMaxLength(100);
            entity.Property(e => e.AttachmentFilename).HasMaxLength(255);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.ContactedAt).HasColumnType("datetime");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.ProcessedAt).HasColumnType("datetime");
            entity.Property(e => e.ResolvedAt).HasColumnType("datetime");
            entity.Property(e => e.Subject).HasMaxLength(200);
        });

        modelBuilder.Entity<TblUserFlag>(entity =>
        {
            entity.HasKey(e => e.UserFlagId).HasName("PK__TblUserF__2AB5267FD8D84702");

            entity.Property(e => e.AdminAction).HasMaxLength(50);
            entity.Property(e => e.AdminReason).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.AdminUser).WithMany(p => p.TblUserFlagAdminUsers)
                .HasForeignKey(d => d.AdminUserId)
                .HasConstraintName("FK_TblUserFlag_AdminUser");

            entity.HasOne(d => d.FlaggedByUser).WithMany(p => p.TblUserFlagFlaggedByUsers)
                .HasForeignKey(d => d.FlaggedByUserId)
                .HasConstraintName("FK_TblUserFlag_FlaggedByUser");

            entity.HasOne(d => d.FlaggedUser).WithMany(p => p.TblUserFlagFlaggedUsers)
                .HasForeignKey(d => d.FlaggedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TblUserFlag_FlaggedUser");
        });

        modelBuilder.Entity<TblUserHoldHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tblUserH__3214EC07EEDA74EE");

            entity.ToTable("tblUserHoldHistories");

            entity.HasIndex(e => e.UserId, "IX_tblUserHoldHistories_UserId");

            entity.HasOne(d => d.ReleasedByAdminNavigation).WithMany(p => p.TblUserHoldHistoryReleasedByAdminNavigations)
                .HasForeignKey(d => d.ReleasedByAdmin)
                .HasConstraintName("FK_tblUserHoldHistories_ReleasedByAdmin");

            entity.HasOne(d => d.User).WithMany(p => p.TblUserHoldHistoryUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblUserHoldHistories_Users");
        });

        modelBuilder.Entity<TblUserReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            entity.ToTable("tblUserReports");

            entity.Property(e => e.ReportId).HasColumnName("ReportID");
            entity.Property(e => e.ActionTaken).HasMaxLength(100);
            entity.Property(e => e.ExchangeId).HasColumnName("ExchangeID");
            entity.Property(e => e.ReportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ReportedUserId).HasColumnName("ReportedUserID");
            entity.Property(e => e.ReporterUserId).HasColumnName("ReporterUserID");
            entity.Property(e => e.ReviewedByAdminId).HasColumnName("ReviewedByAdminID");
            entity.Property(e => e.ReviewedDate).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Exchange).WithMany(p => p.TblUserReports)
                .HasForeignKey(d => d.ExchangeId)
                .HasConstraintName("FK_UserReports_Exchange");

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

        modelBuilder.Entity<TblUserWishlist>(entity =>
        {
            entity.HasKey(e => e.WishlistId).HasName("PK__tblUserW__233189EBCD07F2BE");

            entity.ToTable("tblUserWishlist");

            entity.HasIndex(e => new { e.UserId, e.OfferId }, "UQ_Wishlist_User_Offer").IsUnique();

            entity.Property(e => e.AddedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Offer).WithMany(p => p.TblUserWishlists)
                .HasForeignKey(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Wishlist_Offer");

            entity.HasOne(d => d.User).WithMany(p => p.TblUserWishlists)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Wishlist_User");
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

        modelBuilder.Entity<TokenEmissionSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TokenEmi__3214EC071CA14F88");

            entity.Property(e => e.DailyCap).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.TotalPool).HasColumnType("decimal(18, 4)");
        });

        modelBuilder.Entity<UserGoogleToken>(entity =>
        {
            entity.HasKey(e => e.UserGoogleTokenId).HasName("PK__UserGoog__09CB07C16A6A0688");

            entity.Property(e => e.AccessToken).HasMaxLength(500);
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.RefreshToken).HasMaxLength(500);
        });

        modelBuilder.Entity<UserMiningProgress>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("UserMiningProgress");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.EmittedToday).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.IsMiningAllowed).HasDefaultValue(true);
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

        modelBuilder.Entity<VerificationRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Verifica__3214EC0710FE1CCE");

            entity.Property(e => e.GovernmentIdDocumentPath).HasMaxLength(500);
            entity.Property(e => e.GovernmentIdNumber).HasMaxLength(100);
            entity.Property(e => e.GovernmentIdType).HasMaxLength(50);
            entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.UserId).HasMaxLength(450);
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

        modelBuilder.Entity<TblUser>()
            .HasQueryFilter(u =>
                u.Role != "Admin"
             && u.Role != "Moderator"
             && !u.IsEscrowAccount
             && !u.IsSupportAgent
             && !u.IsSystemReserveAccount
            );

        modelBuilder.Entity<ReviewAggregate>().HasNoKey().ToView(null);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
