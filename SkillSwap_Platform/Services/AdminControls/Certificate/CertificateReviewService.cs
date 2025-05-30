using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.Payment_Gatway;

namespace SkillSwap_Platform.Services.AdminControls.Certificate
{
    public class CertificateReviewService : ICertificateReviewService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<CertificateReviewService> _logger;
        private readonly IEmailService _emailService;
        private readonly ISubscriptionService _subs;
        public CertificateReviewService(
            SkillSwapDbContext db,
            ILogger<CertificateReviewService> logger,
            IEmailService emailService,
            ISubscriptionService subscription)
        {
            _db = db;
            _logger = logger;
            _emailService = emailService;
            _subs = subscription;
        }

        private async Task<PagedResult<CertificateReviewDto>> GetPagedAsync(
            IQueryable<CertificateReviewDto> query, int page, int pageSize)
        {
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<CertificateReviewDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Items = items
            };
        }

        public Task<PagedResult<CertificateReviewDto>> GetPendingCertificatesAsync(int page, int pageSize)
        {
            var baseQuery = from cert in _db.TblUserCertificates
                            where cert.ApprovedDate == null
                             && cert.RejectDate == null
                            join user in _db.TblUsers
                              on cert.UserId equals user.UserId
                            select new CertificateReviewDto
                            {
                                CertificateId = cert.CertificateId,
                                UserName = $"{user.FirstName} {user.LastName}",
                                LoginName = user.UserName,
                                CertificateName = cert.CertificateName,
                                SubmittedDate = cert.SubmittedDate,
                                IsApproved = cert.IsApproved,
                                ApprovedDate = cert.ApprovedDate
                            };

            return GetPagedAsync(baseQuery, page, pageSize);
        }


        public Task<PagedResult<CertificateReviewDto>> GetApprovedCertificatesAsync(int page, int pageSize)
        {
            var baseQuery = from cert in _db.TblUserCertificates
                            where cert.ApprovedDate != null && cert.IsApproved == true
                            join user in _db.TblUsers
                              on cert.UserId equals user.UserId
                            select new CertificateReviewDto
                            {
                                CertificateId = cert.CertificateId,
                                UserName = $"{user.FirstName} {user.LastName}",
                                LoginName = user.UserName,
                                CertificateName = cert.CertificateName,
                                SubmittedDate = cert.SubmittedDate,
                                IsApproved = cert.IsApproved,
                                ApprovedDate = cert.ApprovedDate,
                                Status = CertificateReviewDto.ReviewStatus.Approved
                            };

            return GetPagedAsync(baseQuery, page, pageSize);
        }

        public Task<PagedResult<CertificateReviewDto>> GetRejectedCertificatesAsync(int page, int pageSize)
        {
            var baseQuery = from cert in _db.TblUserCertificates
                            where cert.RejectDate != null && cert.IsApproved == false
                            join user in _db.TblUsers
                              on cert.UserId equals user.UserId
                            select new CertificateReviewDto
                            {
                                CertificateId = cert.CertificateId,
                                UserName = $"{user.FirstName} {user.LastName}",
                                LoginName = user.UserName,
                                CertificateName = cert.CertificateName,
                                SubmittedDate = cert.SubmittedDate,
                                IsApproved = cert.IsApproved,
                                ApprovedDate = cert.ApprovedDate,
                                RejectDate = cert.RejectDate,
                                Status = CertificateReviewDto.ReviewStatus.Rejected

                            };

            return GetPagedAsync(baseQuery, page, pageSize);
        }

        public async Task<CertificateDetailDto> GetCertificateDetailAsync(int certificateId)
        {
            try
            {
                return await _db.TblUserCertificates
                    .Where(c => c.CertificateId == certificateId)
                    .Join(
                        _db.TblUsers,
                        cert => cert.UserId,
                        user => user.UserId,

                        // ← resultSelector here, _not_ in a later .Select
                        (cert, user) => new CertificateDetailDto
                        {
                            CertificateId = cert.CertificateId,
                            UserName = $"{user.FirstName} {user.LastName}",
                            LoginName = user.UserName,
                            CertificateName = cert.CertificateName,
                            SubmittedDate = cert.SubmittedDate,
                            CertificateFrom = cert.CertificateFrom,
                            CompleteDate = cert.CompleteDate,
                            VerificationId = cert.VerificationId,
                            IsApproved = cert.IsApproved,
                            ApprovedDate = cert.ApprovedDate,
                            Status = cert.IsApproved
                                ? CertificateDetailDto.ReviewStatus.Approved
                                : cert.RejectDate != null
                                    ? CertificateDetailDto.ReviewStatus.Rejected
                                    : CertificateDetailDto.ReviewStatus.Pending,
                            ProcessedDateUtc = cert.IsApproved
                                    ? cert.ApprovedDate
                                    : cert.RejectDate,
                            RejectDate = cert.RejectDate,
                            RejectionReason = cert.RejectionReason,
                            CertificateFilePath = cert.CertificateFilePath
                        }
                    )
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading certificate detail for {CertificateId}", certificateId);
                return null;
            }
        }

        public async Task<bool> ApproveCertificateAsync(int certificateId, int adminUserId)
        {
            try
            {
                var cert = await _db.TblUserCertificates.FindAsync(certificateId);
                if (cert == null) return false;

                cert.IsApproved = true;
                cert.ApprovedByAdminId = adminUserId;
                cert.ApprovedDate = DateTime.UtcNow;

                _db.TblUserCertificates.Update(cert);
                await _db.SaveChangesAsync();

                var user = await _db.TblUsers.FindAsync(cert.UserId);
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await _subs.GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] 🎉 Your Certificate Has Been Approved!";
                    var htmlBody = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI, sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
        
        <!-- header -->
        <tr>
          <td style=""background:#00A88F;color:#fff;padding:15px;font-size:20px;text-align:center;"">
            Swapo
          </td>
        </tr>
        
        <!-- body -->
        <tr>
          <td style=""padding:20px;color:#333;line-height:1.5;"">
            <p>Hi <strong>{user.FirstName}</strong>,</p>
            <p>Great news! Your submission for <strong>“{cert.CertificateName}”</strong> was approved on 
               <strong>{cert.ApprovedDate:dd MMM yyyy hh:mm tt} UTC</strong>.</p>
            <p>You can now confidently showcase this credential on your profile:</p>
            <p style=""text-align:center;margin:20px 0;"">
              <a href=""/UserProfile/EditProfile"" 
                 style=""background:#28a745;color:#fff;padding:10px 16px;text-decoration:none;border-radius:4px;"">
                View Certificates
              </a>
            </p>
            <p>If you have any questions, reply to this email or contact 
               <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.</p>
          </td>
        </tr>
        
        <!-- footer -->
        <tr>
          <td style=""background:#00A88F;color:#E0F7F1;padding:10px;text-align:center;font-size:12px;"">
            Congratulations and happy swapping! — The Swapo Team
          </td>
        </tr>
      
      </table>
    </td></tr>
  </table>
</body>
</html>
";

                    await _emailService.SendEmailAsync(
                      user.Email,
                      subject,
                      htmlBody,
                      isBodyHtml: true
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving certificate {CertificateId}", certificateId);
                return false;
            }
        }

        public async Task<bool> RejectCertificateAsync(int certificateId, int adminUserId, string reason)
        {
            try
            {
                var cert = await _db.TblUserCertificates.FindAsync(certificateId);
                if (cert == null) return false;

                // Flag it as reviewed but not approved
                cert.IsApproved = false;
                cert.ApprovedByAdminId = adminUserId;
                cert.RejectDate = DateTime.UtcNow;
                cert.RejectionReason = reason;

                _db.TblUserCertificates.Update(cert);
                await _db.SaveChangesAsync();

                var user = await _db.TblUsers.FindAsync(cert.UserId);
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await _subs.GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] Update on Your Certificate Submission";
                    var htmlBody = $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI, sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""padding:20px;"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#fff;border-collapse:collapse;"">
        
        <!-- header -->
        <tr>
          <td style=""background:#00A88F;color:#fff;padding:15px;font-size:20px;text-align:center;"">
            Swapo
          </td>
        </tr>
        
        <!-- body -->
        <tr>
          <td style=""padding:20px;color:#333;line-height:1.5;"">
            <p>Hi <strong>{user.FirstName}</strong>,</p>
            <p>We’ve reviewed your submission for <strong>“{cert.CertificateName}”</strong>, and unfortunately we couldn’t confirm its authenticity.</p>
            <hr style=""border:none;border-top:1px solid #e0e0e0;""/>
            <p><strong>Reason:</strong></p>
            <blockquote style=""border-left:4px solid #ccc;padding-left:1em;margin:1em 0;"">
              {reason}
            </blockquote>
            <p>Please address the above and <a href=""/UserProfile/EditProfile""
               style=""background:#dc3545;color:#fff;padding:10px 16px;text-decoration:none;border-radius:4px;"">
               View & Update
            </a> your certificate.</p>
            <p>Questions? Reply to this email or <a href=""mailto:swapoorg360@gmail.com"">swapoorg360@gmail.com</a>.</p>
          </td>
        </tr>
        
        <!-- footer -->
        <tr>
          <td style=""background:#00A88F;color:#E0F7F1;padding:10px;text-align:center;font-size:12px;"">
            Thank you for keeping Swapo a trusted community. — The Swapo Team
          </td>
        </tr>
      
      </table>
    </td></tr>
  </table>
</body>
</html>
";

                    await _emailService.SendEmailAsync(
                      user.Email,
                      subject,
                      htmlBody,
                      isBodyHtml: true
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting certificate {CertificateId}", certificateId);
                return false;
            }
        }
    }
}
