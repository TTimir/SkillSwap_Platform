using Google;
using Newtonsoft.Json;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM;
using SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM;
using SkillSwap_Platform.Services.Email;
using VerificationStatus = SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM.VerificationStatus;
using SkillSwap_Platform.Services.Payment_Gatway;

namespace SkillSwap_Platform.Services.ProfileVerification
{
    public class VerificationService : IVerificationService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<VerificationService> _log;
        private readonly IEmailService _emailSender;
        private readonly ISubscriptionService _subs;

        public VerificationService(
            SkillSwapDbContext db,
            IWebHostEnvironment env,
            ILogger<VerificationService> log,
            IEmailService emailService,
            ISubscriptionService subscriptions)
        {
            _db = db;
            _env = env;
            _log = log;
            _emailSender = emailService;
            _subs = subscriptions;
        }

        public async Task SubmitAsync(string userId, SubmitRequestVm vm)
        {
            var uid = int.Parse(userId);

            var sub = await _subs.GetActiveAsync(uid);
            var plan = sub?.PlanName ?? "Freebie";
            var lifetime = await _db.VerificationRequests
                                      .CountAsync(r => r.UserId == userId);
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var thisMonth = await _db.VerificationRequests
                                      .CountAsync(r => r.UserId == userId
                                                    && r.SubmittedAt >= monthStart);

            switch (plan)
            {
                case "Freebie":
                    if (lifetime >= 1)
                        throw new InvalidOperationException(
                            "Free members may only submit one verification request in their lifetime.");
                    break;

                case "Plus":
                case "Pro":
                    if (thisMonth >= 1)
                        throw new InvalidOperationException(
                            "Plus & Pro members may submit one verification request per calendar month.");
                    break;

                case "Growth":
                    if (thisMonth >= 2)
                        throw new InvalidOperationException(
                            "Growth members may submit up to two verification requests per calendar month.");
                    break;
            }

            try
            {
                // create upload folder
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "verifications", userId);
                Directory.CreateDirectory(uploads);

                string SaveFile(IFormFile file)
                {
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var path = Path.Combine(uploads, fileName);
                    using var fs = new FileStream(path, FileMode.Create);
                    file.CopyTo(fs);
                    return $"/uploads/verifications/{userId}/{fileName}";
                }

                var req = new VerificationRequest
                {
                    UserId = userId,
                    GovernmentIdType = vm.GovernmentIdType,
                    GovernmentIdNumber = vm.GovernmentIdNumber,
                    GovernmentIdDocumentPath = SaveFile(vm.GovernmentIdDocument),
                    CertificatesJson = JsonConvert.SerializeObject(
                        vm.Certificates.Select(c => new { c.SkillName, Path = SaveFile(c.CertificateFile) })
                    ),
                    EducationJson = JsonConvert.SerializeObject(
                        vm.EducationRecords.Select(e => new { e.Degree, e.Institution, Path = SaveFile(e.EduProofFile) })
                    ),
                    ExperienceJson = JsonConvert.SerializeObject(
                        vm.ExperienceRecords
                          .Where(e => e.ExpProofFile != null)
                          .Select(e => new { e.Company, e.Role, Path = SaveFile(e.ExpProofFile) })
                    ),
                    Status = 0,
                    SubmittedAt = DateTime.UtcNow
                };

                _db.VerificationRequests.Add(req);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error submitting verification for user {User}", userId);
                throw;
            }
        }

        public async Task<IList<AdminListVm>> GetPendingAsync()
        {
            return await _db.VerificationRequests
                .Where(r => r.Status == 0)
                .OrderBy(r => r.SubmittedAt)
                .Select(r => new AdminListVm
                {
                    Id = r.Id,
                    // match TblUsers.UserId against r.UserId, not r.Id
                    Name = _db.TblUsers
                                   .Where(u => u.UserId.ToString() == r.UserId)
                                  .Select(u => u.FirstName + " " + u.LastName)
                                  .FirstOrDefault(),
                    Username = _db.TblUsers
                                   .Where(u => u.UserId.ToString() == r.UserId)
                                  .Select(u => u.UserName)
                                  .FirstOrDefault(),
                    SubmittedAt = r.SubmittedAt,
                    Status = (Models.ViewModels.ProfileVerificationVM.VerificationStatus)r.Status
                })
                .ToListAsync();
        }

        public async Task<AdminDetailsVm> GetDetailsAsync(long requestId)
        {
            var r = await _db.VerificationRequests.FindAsync(requestId);
            if (r == null) throw new KeyNotFoundException();

            // deserialize JSON back into VM lists
            var certsRaw = JsonConvert.DeserializeObject<
    IList<dynamic>>(r.CertificatesJson);
            var edusRaw = JsonConvert.DeserializeObject<
                IList<dynamic>>(r.EducationJson);
            var expsRaw = JsonConvert.DeserializeObject<
                IList<dynamic>>(r.ExperienceJson);

            // now project to AdminDetailsVm.CertificateRecord etc.
            var certs = certsRaw.Select(c => new AdminDetailsVm.CertificateRecord
            {
                SkillName = (string)c.SkillName,
                CertificateFilePath = (string)c.Path
            }).ToList();

            var edus = edusRaw.Select(e => new AdminDetailsVm.EducationRecord
            {
                Degree = (string)e.Degree,
                Institution = (string)e.Institution,
                EduProofFilePath = (string)e.Path
            }).ToList();

            var exps = expsRaw.Select(e => new AdminDetailsVm.ExperienceRecord
            {
                Company = (string)e.Company,
                Role = (string)e.Role,
                ExpProofFilePath = (string)e.Path
            }).ToList();

            return new AdminDetailsVm
            {
                Id = r.Id,
                SubmittedByUser = _db.TblUsers
                                   .Where(u => u.UserId.ToString() == r.UserId)
                                  .Select(u => u.FirstName + " " + u.LastName)
                                  .FirstOrDefault(),
                SubmittedByUsername = _db.TblUsers
                                   .Where(u => u.UserId.ToString() == r.UserId)
                                  .Select(u => u.UserName)
                                  .FirstOrDefault(),
                SubmittedAt = r.SubmittedAt,
                Status = (Models.ViewModels.ProfileVerificationVM.VerificationStatus)r.Status,
                ReviewedAt = r.ReviewedAt,
                ReviewedByUsername = r.ReviewedByUserId,
                ReviewComments = r.ReviewComments,
                GovernmentIdType = r.GovernmentIdType,
                GovernmentIdNumber = r.GovernmentIdNumber,
                // we’ll pass only paths for display
                GovernmentIdDocumentPath = r.GovernmentIdDocumentPath, // not used
                Certificates = certs,
                EducationRecords = edus,
                ExperienceRecords = exps
            };
        }

        public async Task ApproveAsync(long requestId, string adminId, string comments)
        {
            await ReviewAsync(requestId, adminId, VerificationStatus.Approved, comments);

            // 1) Load & update the request
            var req = await _db.VerificationRequests.FindAsync(requestId);
            if (req == null) throw new KeyNotFoundException($"Request {requestId} not found.");

            // 2) Mark the user as verified
            if (int.TryParse(req.UserId, out var userIdInt))
            {
                var user = await _db.TblUsers.FindAsync(userIdInt);
                if (user != null)
                    user.IsVerified = true;
            }

            // 3) Persist both changes
            await _db.SaveChangesAsync();

            await SendApprovalEmailAsync(requestId);
        }

        public async Task RejectAsync(long requestId, string adminId, string comments)
        {
            await ReviewAsync(requestId, adminId, VerificationStatus.Rejected, comments);

            // 1) Load & update the request
            var req = await _db.VerificationRequests.FindAsync(requestId);
            if (req == null) throw new KeyNotFoundException($"Request {requestId} not found.");

            // 2) Mark the user as verified
            if (int.TryParse(req.UserId, out var userIdInt))
            {
                var user = await _db.TblUsers.FindAsync(userIdInt);
                if (user != null)
                    user.IsVerified = false;
            }

            // 3) Persist both changes
            await _db.SaveChangesAsync();

            await SendRejectionEmailAsync(requestId, comments);
        }

        private async Task ReviewAsync(long id, string adminId, VerificationStatus status, string comments)
        {
            try
            {
                var req = await _db.VerificationRequests.FindAsync(id);
                if (req == null) throw new KeyNotFoundException();
                req.Status = (int)status;
                req.ReviewComments = comments;
                req.ReviewedAt = DateTime.UtcNow;
                req.ReviewedByUserId = adminId;
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error reviewing request {RequestId}", id);
                throw;
            }
        }

        // Services/ProfileVerification/VerificationService.cs
        public async Task<IList<HistoryItemVm>> GetHistoryAsync()
        {
            // 1) load all requests (including pending if you want)
            var requests = await _db.VerificationRequests
                .Select(r => new {
                    r.Id,
                    r.UserId,
                    r.SubmittedAt,
                    r.Status,
                    r.ReviewedAt,
                    r.ReviewComments
                })
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync();

            // 2) load all users (or at least those referenced)
            var users = await _db.TblUsers.ToListAsync();

            // 3) build the combined timeline
            var history = new List<HistoryItemVm>();
            foreach (var req in requests)
            {
                // find the matching user
                var user = users.FirstOrDefault(u => u.UserId.ToString() == req.UserId);

                // submission event
                history.Add(new HistoryItemVm
                {
                    RequestId = req.Id,
                    UserId = req.UserId,
                    DisplayName = user != null
                                    ? $"{user.FirstName} {user.LastName}"
                                    : req.UserId,
                    Username = user?.UserName,
                    Timestamp = req.SubmittedAt.ToLocalTime(),
                    Event = "Submitted",
                    Comments = null
                });

                // review event (if any)
                if (req.Status != (int)VerificationStatus.Pending)
                {
                    history.Add(new HistoryItemVm
                    {
                        RequestId = req.Id,
                        UserId = req.UserId,
                        DisplayName = user != null
                                        ? $"{user.FirstName} {user.LastName}"
                                        : req.UserId,
                        Username = user?.UserName,
                        Timestamp = req.ReviewedAt?.ToLocalTime() ?? DateTime.MinValue,
                        Event = req.Status == (int)VerificationStatus.Approved
                                        ? "Approved"
                                        : "Rejected",
                        Comments = req.ReviewComments
                    });
                }
            }

            return history
                .OrderByDescending(h => h.Timestamp)
                .ToList();
        }

        public async Task RevokeAsync(long requestId, string adminId, string comments)
        {
            // 1) fetch the original request
            var req = await _db.VerificationRequests.FindAsync(requestId);
            if (req == null) throw new KeyNotFoundException();

            // 2) only allow revoke if it was approved
            if (req.Status != (int)VerificationStatus.Approved)
                throw new InvalidOperationException("Can only revoke a previously approved request.");

            // 3) mark it as “Rejected” (or a new “Revoked” status if you prefer)
            req.Status = (int)VerificationStatus.Rejected;
            req.ReviewComments = comments;
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedByUserId = adminId;

            // 4) unset the user’s verified badge
            var user = await _db.TblUsers
                        .SingleOrDefaultAsync(u => u.UserId.ToString() == req.UserId);
            if (user != null)
            {
                user.IsVerified = false;
            }

            await _db.SaveChangesAsync();

            // 4) send them an email
            await SendRevokeEmailAsync(requestId, comments);
        }

        private async Task SendApprovalEmailAsync(long requestId)
        {
            // 1) load the request and user
            var req = await _db.VerificationRequests.FindAsync(requestId);
            if (req == null) return;

            var user = await _db.TblUsers
                .SingleOrDefaultAsync(u => u.UserId.ToString() == req.UserId);

            if (user == null || string.IsNullOrEmpty(user.Email))
                return;

            // 2) build a friendly message
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
            var subjectApproved = $"[{supportLabel} · {sla}] 🎉 You’re Verified on Swapo!";
            var bodyApproved = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">
      
      <!-- Header: Blue -->
      <tr>
        <td style=""background:#0066CC;color:#ffffff;padding:15px;font-size:18px;text-align:center;font-weight:bold;"">
          Swapo Verification
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.5;"">
          <h3 style=""margin-top:0;"">Your Swapo Verification Has Been Approved!</h3>
          <h2 style=""color:#0066CC;margin-bottom:15px;"">Congratulations, {user.FirstName}!</h2>
          <p>We’re thrilled to let you know that your verification request <strong>#{requestId}</strong> has been <span style=""color:green;"">approved</span>.</p>

          <p style=""font-size:1.1em;margin-top:20px;"">What this means for you:</p>
          <ul style=""margin:0;padding-left:1.2em;"">
            <li><strong>Verified badge</strong> added to your profile — stand out instantly.</li>
            <li>Boost your visibility to learners and collaborators worldwide.</li>
            <li>Unlock exclusive features and get featured in our community highlights.</li>
          </ul>

          <p style=""margin-top:20px;"">Keep sharing your expertise and inspiring others—we can’t wait to see what you swap next!</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Questions? <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">Contact Support</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, subjectApproved, bodyApproved, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send approval email for request {RequestId}", requestId);
            }
        }

        private async Task SendRejectionEmailAsync(long requestId, string comments)
        {
            // 1) load the request and user
            var req = await _db.VerificationRequests.FindAsync(requestId);
            if (req == null) return;

            var user = await _db.TblUsers
                .SingleOrDefaultAsync(u => u.UserId.ToString() == req.UserId);

            if (user == null || string.IsNullOrEmpty(user.Email))
                return;

            // 2) build a constructive message
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
            var subjectUpdate = $"[{supportLabel} · {sla}] Update on Your Swapo Verification Request";
            var commentSection = string.IsNullOrWhiteSpace(comments)
                ? ""
                : $@"<blockquote style=""border-left:3px solid #ccc;padding-left:10px;color:#555;margin:15px 0;"">
          <em>{comments}</em>
        </blockquote>";

            var bodyUpdate = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">
      
      <!-- Header: Orange -->
      <tr>
        <td style=""background:#CC3300;color:#ffffff;padding:15px;font-size:18px;text-align:center;font-weight:bold;"">
          Verification Update Required
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.6;"">
          <h2 style=""margin-top:0;color:#CC3300;"">Hey {user.FirstName},</h2>
          <p>Thanks for submitting documents for verification <strong>#{requestId}</strong>. You’re almost there!</p>

          {commentSection}

          <p><em>To complete your verification:</em></p>
          <ol style=""padding-left:1.2em;margin:0 0 20px;"">
            <li>Ensure your document scans are clear, legible, and show your name & date.</li>
            <li>Confirm that your certificates match the skills listed in your profile.</li>
            <li>Re-submit using our verification form and we’ll review it promptly.</li>
          </ol>

          <p>Once verified, you’ll unlock your shiny badge and stand out in our community. Let’s get you across the finish line!</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Need help? <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">Contact Support</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, subjectUpdate, bodyUpdate, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send rejection email for request {RequestId}", requestId);
            }
        }

            private async Task SendRevokeEmailAsync(long requestId, string comments)
            {
                // 1) load the request and the user
                var req = await _db.VerificationRequests.FindAsync(requestId);
                if (req == null) return;

                var user = await _db.TblUsers
                    .SingleOrDefaultAsync(u => u.UserId.ToString() == req.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email))
                    return;

            // 2) build a thoughtful, motivating message
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
            var subjectRevoked = $"[{supportLabel} · {sla}] Important Update on Your Swapo Verification";
            var commentSection = string.IsNullOrWhiteSpace(comments)
                ? ""
                : $@"<blockquote style=""border-left:3px solid #ccc;padding:10px;color:#555;margin:15px 0;"">
          <em>{comments}</em>
        </blockquote>";

            var bodyRevoked = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f2f2f2;font-family:Segoe UI,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr><td align=""center"" style=""padding:20px;"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#ffffff;border-collapse:collapse;"">
      
      <!-- Header: Orange/Red -->
      <tr>
        <td style=""background:#CC3300;color:#ffffff;padding:15px;font-size:18px;text-align:center;font-weight:bold;"">
          Verification Status Update
        </td>
      </tr>

      <!-- Body -->
      <tr>
        <td style=""padding:20px;color:#333;line-height:1.6;"">
          <h2 style=""margin-top:0;color:#CC3300;"">Hi {user.FirstName},</h2>
          <p>We wanted to let you know that your verified status on Swapo (request <strong>#{requestId}</strong>) has been <span style=""color:#CC3300;"">revoked</span>.</p>

          {commentSection}

          <p>Here’s how to get back on track:</p>
          <ol style=""padding-left:1.2em;margin:0 0 20px;"">
            <li>Review the feedback above to understand why your badge was removed.</li>
            <li>Gather any additional or clearer documentation that highlights your credentials.</li>
            <li>Re-submit your verification and we’ll fast-track the review.</li>
          </ol>

          <p>Remember: a verified badge sets you apart in our global community. We believe in your expertise and can’t wait to help you regain that trusted status!</p>
        </td>
      </tr>

      <!-- Footer: Green -->
      <tr>
        <td style=""background:#00A88F;padding:10px 20px;text-align:center;color:#E0F7F1;font-size:12px;"">
          Questions? <a href=""mailto:swapoorg360@gmail.com"" style=""color:#ffffff;text-decoration:underline;"">Contact Support</a>.
        </td>
      </tr>

    </table>
  </td></tr></table>
</body>
</html>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, subjectRevoked, bodyRevoked, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send revoke email for request {RequestId}", requestId);
            }
        }
    }
}