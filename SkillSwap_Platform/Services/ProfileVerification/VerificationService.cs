using Google;
using Newtonsoft.Json;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM;
using SkillSwap_Platform.Models.ViewModels.ProfileVerificationVM;
using SkillSwap_Platform.Services.Email;
using VerificationStatus = SkillSwap_Platform.Models.ViewModels.ProfileVerifivationVM.VerificationStatus;

namespace SkillSwap_Platform.Services.ProfileVerification
{
    public class VerificationService : IVerificationService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<VerificationService> _log;
        private readonly IEmailService _emailSender;

        public VerificationService(
            SkillSwapDbContext db,
            IWebHostEnvironment env,
            ILogger<VerificationService> log,
            IEmailService emailService)
        {
            _db = db;
            _env = env;
            _log = log;
            _emailSender = emailService;
        }

        public async Task SubmitAsync(string userId, SubmitRequestVm vm)
        {
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
            var subject = "🎉 You’re Verified on SkillSwap!";
            var body = $@"
                <div style='font-family:Segoe UI, sans-serif; color:#333; line-height:1.5em;'>
                  <h3>Your SkillSwap Verification Has Been Approved!</h3>
                  <h2 style='color:#0066CC;'>Congratulations, {user.FirstName}!</h2>
                  <p>We’re thrilled to let you know that your verification request <strong>#{requestId}</strong> has been <span style='color:green;'>approved</span>.</p>

                  <p style='font-size:1.1em;'>What this means for you:</p>
                  <ul>
                    <li><strong>Verified badge</strong> added to your SkillSwap profile — stand out and build instant trust.</li>
                    <li>Boost your visibility to learners and collaborators across our global community.</li>
                    <li>Unlock exclusive opportunities — top swappers often get featured in our newsletters and social channels.</li>
                  </ul>

                  <p>Keep sharing your expertise, inspiring others, and unlocking new skills together. We can’t wait to see what you swap next!</p>

                  <p style='margin-top:40px;'>Warmly,<br/>
                  <strong>The SkillSwap Team</strong></p>
                </div>
            ";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, subject, body);
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
            var subject = "Update on Your SkillSwap Verification Request";
            var commentSection = string.IsNullOrWhiteSpace(comments)
                ? ""
                : $"<blockquote style='border-left:3px solid #ccc; padding-left:10px; color:#555;'>" +
                  $"<em>{comments}</em></blockquote>";

            var body = $@"
              <div style='font-family:Segoe UI, sans-serif; color:#333; line-height:1.6em;'>
                <h2 style='color:#CC3300;'>Hey {user.FirstName},</h2>
            
                <p>First off, thank you for taking that extra step and sharing your documents for verification <strong>#{requestId}</strong>. You’re already showing the dedication that sets top SkillSwap members apart!</p>
            
                {commentSection}
            
                <p><em>Every great journey has a few checkpoints…</em> these tweaks will put you right over the line:</p>
                <ol>
                  <li><strong>Fine-tune</strong> your document scans—clear, legible, with your name and date front-and-center.</li>
                  <li><strong>Double-check</strong> that your certificates match the skills listed in your profile.</li>
                  <li><strong>Re-submit</strong> using our simple form and we’ll sprint to review it.</li>
                </ol>
            
                <p>Once you’re verified, you’ll unlock a <strong>shiny badge</strong> on your profile that instantly signals trust and expertise to everyone in our community. Think of it as your personal seal of credibility!</p>
            
                <p>You’ve built valuable skills—now let them shine. Keep swapping knowledge, forging connections, and expanding your horizons.</p>
            
                <p>We’re rooting for you every step of the way. Let’s get you verified—your next big opportunity awaits!</p>
            
                <p style='margin-top:40px;'>With enthusiasm,<br/>
                <strong>The SkillSwap Team</strong></p>
              </div>
            ";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, subject, body);
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
                var subject = "Important Update on Your SkillSwap Verification";
                var commentSection = string.IsNullOrWhiteSpace(comments)
                    ? ""
                    : $"<blockquote style='border-left:3px solid #ccc; padding:10px; color:#555;'>" +
                      $"<em>{comments}</em></blockquote>";

                var body = $@"
                  <div style='font-family:Segoe UI, sans-serif; color:#333; line-height:1.6em;'>
                    <h2 style='color:#CC3300;'>Hi {user.FirstName},</h2>
                    <p>We wanted to let you know that your verified status on SkillSwap (request <strong>#{requestId}</strong>) has been <span style='color:#CC3300;'>revoked</span>.</p>

                    {commentSection}

                    <p>Here’s how to get back on track:</p>
                    <ol>
                      <li>Review the feedback above to understand why your badge was removed.</li>
                      <li>Gather any additional or clearer documentation that highlights your credentials.</li>
                      <li>Re-submit your verification and we’ll fast-track the review.</li>
                    </ol>

                    <p>Remember: a verified badge sets you apart in our global community. We believe in your expertise and can’t wait to help you regain that trusted status!</p>

                    <p style='margin-top:40px;'>All the best,<br/>
                    <strong>The SkillSwap Team</strong></p>
                  </div>
                ";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, subject, body);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send revoke email for request {RequestId}", requestId);
            }
        }
    }
}