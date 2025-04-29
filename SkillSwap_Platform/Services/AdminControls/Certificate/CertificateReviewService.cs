using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;

namespace SkillSwap_Platform.Services.AdminControls.Certificate
{
    public class CertificateReviewService : ICertificateReviewService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<CertificateReviewService> _logger;
        private readonly IEmailService _emailService;
        public CertificateReviewService(
            SkillSwapDbContext db,
            ILogger<CertificateReviewService> logger, 
            IEmailService emailService)
        {
            _db = db;
            _logger = logger;
            _emailService = emailService;
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
                                ApprovedDate = cert.ApprovedDate
                            };

            return GetPagedAsync(baseQuery, page, pageSize);
        }

        public Task<PagedResult<CertificateReviewDto>> GetRejectedCertificatesAsync(int page, int pageSize)
        {
            var baseQuery = from cert in _db.TblUserCertificates
                            where cert.ApprovedDate != null && cert.IsApproved == false
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
                            RejectionReason = cert.RejectionReason
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
                    var subject = "🎉 Your Certificate Has Been Approved!";
                    var body = $@"
                        <p>Hi {user.FirstName},</p>
                        <p>Great news! Your submission for <strong>“{cert.CertificateName}”</strong> was approved on <strong>{cert.ApprovedDate:dd MMM yyyy hh:mm tt} UTC</strong>.</p>
                        <p>You can now confidently showcase this credential on your profile:</p>
                        <p style=""text-align:center;"">
                          <a href=""/UserProfile/EditProfile"" style=""padding:8px 12px; background:#28a745; color:white; text-decoration:none; border-radius:4px;"">
                            View Certificates
                          </a>
                        </p>
                        <p>If you have any questions, just reply to this email or reach out to 
                           <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.</p>
                        <p>Congratulations and happy swapping!<br/>— The SkillSwap Team</p>";

                    await _emailService.SendEmailAsync(
                        to: user.Email,
                        subject: subject,
                        body: body,
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
                cert.ApprovedDate = DateTime.UtcNow;
                cert.RejectionReason = reason;

                _db.TblUserCertificates.Update(cert);
                await _db.SaveChangesAsync();

                var user = await _db.TblUsers.FindAsync(cert.UserId);
                if (user != null)
                {
                    var subject = "Update on Your Certificate Submission";
                    var body = $@"
                        <p>Hi {user.FirstName},</p>
                        <p>We’ve reviewed your submission for <strong>“{cert.CertificateName}”</strong>, and unfortunately our verification process was unable to confirm the authenticity of your <strong>“{cert.CertificateName}”</strong> certificate.</p>
                        <hr/>
                        <p><strong>Reason:</strong></p>
                        <blockquote style=""border-left:4px solid #ccc; padding-left:1em; margin:1em 0;"">
                          {reason}
                        </blockquote>
                        <p>We encourage you to address the points above and resubmit. You can manage your certificates here:</p>
                        <p style=""text-align:center;"">
                          <a href=""/UserProfile/EditProfile"" style=""padding:8px 12px; background:#dc3545; color:white; text-decoration:none; border-radius:4px;"">
                            View & Update
                          </a>
                        </p>
                        <p>If you need more detail or assistance, simply hit reply or email us at 
                            <a href=""mailto:skillswap360@gmail.com"">skillswap360@gmail.com</a>.</p>
                        <p>Thank you for your understanding and keeping SkillSwap a trusted community.<br/>— The SkillSwap Team</p>";

                    await _emailService.SendEmailAsync(
                        to: user.Email,
                        subject: subject,
                        body: body,
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
