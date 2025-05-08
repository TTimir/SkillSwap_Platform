using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Models.ViewModels.MeetingVM;
using SkillSwap_Platform.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Services.NotificationTrack;
using Google.Apis.Calendar.v3.Data;
using System.Text;

namespace SkillSwap_Platform.Controllers
{
    public class ExchangeInPersonController : Controller
    {
        private readonly SkillSwapDbContext _context;
        private readonly ILogger<ExchangeInPersonController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notif;

        public ExchangeInPersonController(SkillSwapDbContext context, ILogger<ExchangeInPersonController> logger, IWebHostEnvironment env, INotificationService notif)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _notif = notif;
        }

        #region Schedule Meeting

        // GET: ScheduleInPerson - Display the scheduling form.
        public async Task<IActionResult> ScheduleInPerson(int exchangeId, int otherUserId)
        {
            // You can load exchange details to prefill the form or display header info.
            var exchange = await _context.TblExchanges.FindAsync(exchangeId);
            if (exchange == null)
            {
                return RedirectToAction("EP404", "EP");
            }

            // Build the view model with a default scheduled time (e.g., one hour later).
            var model = new ScheduleInPersonVM
            {
                ExchangeId = exchangeId,
                OtherUserId = otherUserId,
                ScheduledDateTime = DateTime.UtcNow.AddHours(1), // default value
                MeetingDurationMinutes = 60
            };
            return View(model);
        }

        // POST: ScheduleInPerson - Process the submitted meeting details.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleInPerson(ScheduleInPersonVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Ensure the current user is a participant.
                int currentUserId = GetUserId();

                // INSERT a fresh record:
                var newMeeting = new TblInPersonMeeting
                {
                    ExchangeId = model.ExchangeId,
                    CreatedByUserId = currentUserId,
                    CreatedDate = DateTime.UtcNow,
                    MeetingScheduledDateTime = model.ScheduledDateTime,
                    MeetingLocation = model.Location,
                    MeetingNotes = model.Notes,
                    InpersonMeetingDurationMinutes = model.MeetingDurationMinutes,
                    // generate two new OTPs:
                    InpersonMeetingOtpOfferOwner = GenerateOtp(),
                    InpersonMeetingOtpOtherParty = GenerateOtp()
                };
                _context.TblInPersonMeetings.Add(newMeeting);
                await _context.SaveChangesAsync();

                // Load the related exchange record (to retrieve participant IDs)
                var exchange = await _context.TblExchanges.FindAsync(model.ExchangeId);
                if (exchange == null)
                    return RedirectToAction("EP404", "EP");

                // Update the exchange record with meeting mode and OTP info.
                exchange.IsInPersonExchange = true;
                exchange.IsInOnlineExchange = false;
                exchange.IsInpersonMeetingVerifiedByOfferOwner = false;
                exchange.IsInpersonMeetingVerifiedByOtherParty = false;
                exchange.IsMeetingEnded = false; 
                await _context.SaveChangesAsync();

                // Decide which OTP the current user should share.
                string otpToShare = currentUserId == exchange.OfferOwnerId
                    ? newMeeting.InpersonMeetingOtpOfferOwner
                    : newMeeting.InpersonMeetingOtpOtherParty;

                // Create a system-generated notification message (the OTP itself is hidden).
                var systemMessage = new TblMessage
                {
                    // The sender is the current user.
                    SenderUserId = currentUserId,
                    ReceiverUserId = model.OtherUserId,
                    Content = BuildInPersonNotificationContent(model, newMeeting, currentUserId),
                    SentDate = DateTime.UtcNow,
                    MessageType = "InPersonMeetNotification",
                    ExchangeId = model.ExchangeId,
                };
                _context.TblMessages.Add(systemMessage);
                await _context.SaveChangesAsync();

                // Create and store a history record for scheduling the meeting.
                var schedulingHistory = new TblExchangeHistory
                {
                    ExchangeId = exchange.ExchangeId,
                    OfferId = exchange.OfferId,
                    ActionType = "In-Person Meeting Scheduled",
                    ChangedStatus = "Scheduled",
                    ChangedBy = currentUserId,
                    ChangeDate = DateTime.UtcNow,
                    Reason = $"Meeting scheduled for {model.ScheduledDateTime.ToLocalTime():dd MMMM, yyyy HH:mm} at location '{model.Location}'. Notes: {model.Notes}"
                };
                _context.TblExchangeHistories.Add(schedulingHistory);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // log notification:
                await _notif.AddAsync(new TblNotification
                {
                    UserId = GetUserId(),
                    Title = "In-Person Meeting Scheduled",
                    Message = "Meeting for your in-person meet scheduled.",
                    Url = Url.Action("Conversation", "Messaging", new { exchangeId = model.ExchangeId })
                });

                TempData["SuccessMessage"] = "In-person meeting scheduled successfully. A notification has been sent to the other party.";
                return RedirectToAction("Conversation", "Messaging", new { exchangeId = model.ExchangeId });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error scheduling in-person meeting for exchange {ExchangeId}", model.ExchangeId);
                TempData["ErrorMessage"] = "An error occurred while scheduling the meeting.";
                return View(model);
            }
        }

        // Helper to build the HTML content
        private string BuildInPersonNotificationContent(
            ScheduleInPersonVM model,
            TblInPersonMeeting meeting,
            int currentUserId)
        {
            // hide actual OTP in content
            var safeOtp = "******";
            var sb = new StringBuilder();
            sb.Append("<div class='notification inperson'>")
              .Append($"<strong>Scheduled:</strong> {model.ScheduledDateTime.ToLocalTime():dd MMM yyyy HH:mm}<br/>")
              .Append($"<strong>Location:</strong> {model.Location}<br/>");
            if (!string.IsNullOrEmpty(model.Notes))
                sb.Append($"<strong>Notes:</strong> {model.Notes}<br/>");
            sb.Append($"<strong>OTP:</strong> {safeOtp}")
              .Append("</div>");
            return sb.ToString();
        }

        #endregion

        #region OTP Verification and Retrieval

        // POST: VerifyMeetingByOTP - Verifies the OTP provided by the user.
        // In this design, each party must enter the OTP that was generated for the other party.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyMeetingByOTP(int exchangeId, string otp)
        {
            try
            {
                int currentUserId = GetUserId();

                // Retrieve the in-person meeting record for this exchange.
                var meetingRecord = await _context.TblInPersonMeetings
                    .Where(m => m.ExchangeId == exchangeId)
                    .OrderByDescending(m => m.CreatedDate)
                    .FirstOrDefaultAsync();

                if (meetingRecord == null)
                {
                    return Json(new { success = false, error = "In-person meeting record not found." });
                }
                // Also load the exchange record for participant verification.
                var exchange = await _context.TblExchanges.FindAsync(exchangeId);
                if (exchange == null)
                    return Json(new { success = false, error = "Exchange not found." });

                if ((exchange.OtherUserId ?? 0) != currentUserId && (exchange.OfferOwnerId ?? 0) != currentUserId)
                {
                    return Json(new { success = false, error = "Not authorized." });
                }

                // Check if the current user has already verified.
                if (currentUserId == exchange.OfferOwnerId && meetingRecord.IsInpersonMeetingVerifiedByOfferOwner)
                {
                    return Json(new { success = true, fullyVerified = meetingRecord.IsInpersonMeetingVerified, message = "You have already verified your OTP; waiting for the other party." });
                }
                if (currentUserId == exchange.OtherUserId && meetingRecord.IsInpersonMeetingVerifiedByOtherParty)
                {
                    return Json(new { success = true, fullyVerified = meetingRecord.IsInpersonMeetingVerified, message = "You have already verified your OTP; waiting for the other party." });
                }

                // figure out which OTP is “mine” and which is “theirs”
                bool amOwner = currentUserId == (await _context.TblExchanges.FindAsync(exchangeId))!.OfferOwnerId;
                var myOtp = amOwner ? meetingRecord.InpersonMeetingOtpOfferOwner : meetingRecord.InpersonMeetingOtpOtherParty;
                var theirOtp = amOwner ? meetingRecord.InpersonMeetingOtpOtherParty : meetingRecord.InpersonMeetingOtpOfferOwner;

                // explicit check for “own” OTP
                if (otp == myOtp)
                    return Json(new
                    {
                        success = false,
                        error = "That code is your own OTP. Please enter the OTP the other party shared with you."
                    });

                // Validate the OTP based on the current user's role.
                if (currentUserId == exchange.OfferOwnerId)
                {
                    if (meetingRecord.InpersonMeetingOtpOtherParty == otp)
                    {
                        meetingRecord.IsInpersonMeetingVerifiedByOfferOwner = true;
                        meetingRecord.InpersonMeetingVerifiedDateOfferOwner = DateTime.UtcNow;
                    }
                    else
                    {
                        return Json(new { success = false, error = "OTP mismatch. Please check the OTP provided by the other party." });
                    }
                }
                else if (currentUserId == exchange.OtherUserId)
                {
                    if (meetingRecord.InpersonMeetingOtpOfferOwner == otp)
                    {
                        meetingRecord.IsInpersonMeetingVerifiedByOtherParty = true;
                        meetingRecord.InpersonMeetingVerifiedDateOtherParty = DateTime.UtcNow;
                    }
                    else
                    {
                        return Json(new { success = false, error = "OTP mismatch. Please check the OTP provided by the other party." });
                    }
                }
                await _context.SaveChangesAsync();

                bool fullyVerified = meetingRecord.IsInpersonMeetingVerifiedByOfferOwner && meetingRecord.IsInpersonMeetingVerifiedByOtherParty;
                if (fullyVerified)
                {
                    meetingRecord.IsInpersonMeetingVerified = true;
                    meetingRecord.InpersonMeetingVerifiedDate = DateTime.UtcNow;

                    string verificationNote = "Meeting verified by both parties via OTP.";
                    string reason = $"Meeting Mode: {exchange.ExchangeMode}; Scheduled At: {meetingRecord.MeetingScheduledDateTime:dd MMMM, yyyy HH:mm}; Location: {meetingRecord.MeetingLocation}; Notes: {meetingRecord.MeetingNotes}";

                    // Log the meeting verification in the exchange history.
                    var historyRecord = new TblExchangeHistory
                    {
                        ExchangeId = exchange.ExchangeId,
                        OfferId = exchange.OfferId,
                        ActionType = "In-Person Meeting",
                        ChangedStatus = "Verified",
                        ChangedBy = currentUserId,
                        ChangeDate = DateTime.UtcNow,
                        Reason = $"Meeting verified by both parties. {reason}",
                        MeetingVerifiedDate = DateTime.UtcNow,
                        VerificationNote = verificationNote
                    };
                    _context.TblExchangeHistories.Add(historyRecord);

                    // Update fields in TblExchange as well
                    exchange.IsInpersonMeetingVerifiedByOfferOwner = meetingRecord.IsInpersonMeetingVerifiedByOfferOwner;
                    exchange.IsInpersonMeetingVerifiedByOtherParty = meetingRecord.IsInpersonMeetingVerifiedByOtherParty;
                    exchange.IsInpersonMeetingVerified = true;
                }
                else
                {
                    exchange.IsInpersonMeetingVerifiedByOfferOwner = meetingRecord.IsInpersonMeetingVerifiedByOfferOwner;
                    exchange.IsInpersonMeetingVerifiedByOtherParty = meetingRecord.IsInpersonMeetingVerifiedByOtherParty;
                }
                await _context.SaveChangesAsync();

                // log notification:
                await _notif.AddAsync(new TblNotification
                {
                    UserId = GetUserId(),
                    Title = "In-Person Meeting Verified",
                    Message = "Meeting for your in-person meet is varified.",
                    Url = Url.Action("Conversation", "Messaging"),
                });

                return Json(new
                {
                    success = true,
                    fullyVerified,
                    message = fullyVerified
                        ? "Meeting has been verified by both parties."
                        : "Your OTP verification has been recorded; waiting for the other party."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying meeting OTP for exchange {ExchangeId}", exchangeId);
                return Json(new { success = false, error = "An error occurred while verifying the OTP." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetMyMeetingOTP(int exchangeId)
        {
            try
            {
                int currentUserId = GetUserId();

                var meetingRecord = await _context.TblInPersonMeetings
                    .Where(m => m.ExchangeId == exchangeId)
                    .OrderByDescending(m => m.CreatedDate)
                    .FirstOrDefaultAsync();

                if (meetingRecord == null)
                {
                    return Json(new { success = false, error = "In-person meeting record not found." });
                }
                var exchange = await _context.TblExchanges.FindAsync(exchangeId);
                if (exchange == null)
                    return Json(new { success = false, error = "Exchange not found." });
                if ((exchange.OtherUserId ?? 0) != currentUserId && (exchange.OfferOwnerId ?? 0) != currentUserId)
                {
                    return Json(new { success = false, error = "Not authorized." });
                }

                string myOtp = currentUserId == exchange.OfferOwnerId
                    ? meetingRecord.InpersonMeetingOtpOfferOwner
                    : meetingRecord.InpersonMeetingOtpOtherParty;

                return Json(new { success = true, otp = myOtp });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving meeting OTP for exchange {ExchangeId}", exchangeId);
                return Json(new { success = false, error = "An error occurred while retrieving the OTP." });
            }
        }
        #endregion

        #region Upload Start Meeting Proof

        // Action: UploadMeetingProof (kept unchanged for brevity)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadMeetingProof(ProofOfMeetingVM model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Incomplete end proof data.";
                return RedirectToAction("Conversation", "Messaging");
            }

            var base64Data = model.CapturedProof.Substring(model.CapturedProof.IndexOf(",") + 1);
            byte[] imageBytes = Convert.FromBase64String(base64Data);

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inpersonMeetingProof");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + ".png";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

            string fileUrl = SaveCapturedProof(model.CapturedProof);

            int currentUserId = GetUserId();
            string currentUsername = User.Identity?.Name ?? "Unknown";

            // Retrieve the meeting record.
            var proofRecord = await _context.TblInpersonMeetingProofs.FirstOrDefaultAsync(m => m.ExchangeId == model.ExchangeId);
            if (proofRecord == null)
            {
                proofRecord = new TblInpersonMeetingProof
                {
                    ExchangeId = model.ExchangeId,
                    CreatedDate = DateTime.UtcNow
                };
                _context.TblInpersonMeetingProofs.Add(proofRecord);
            }

            // Update the meeting record with end proof details.
            proofRecord.StartProofImageUrl = fileUrl;
            proofRecord.StartProofDateTime = model.ProofDateTime;
            proofRecord.StartProofLocation = model.ProofLocation;

            await _context.SaveChangesAsync();

            // Add a history record for the end proof submission.
            var historyRecord = new TblExchangeHistory
            {
                ExchangeId = model.ExchangeId,
                ActionType = "Meeting Proof",
                ChangedStatus = "Start Proof Submitted",
                ChangedBy = currentUserId,
                ChangeDate = DateTime.UtcNow,
                Reason = $"Start proof submitted at {DateTime.UtcNow:dd MMMM, yyyy HH:mm} from location: {model.ProofLocation}."
            };
            _context.TblExchangeHistories.Add(historyRecord);
            await _context.SaveChangesAsync();

            // log notification:
            await _notif.AddAsync(new TblNotification
            {
                UserId = GetUserId(),
                Title = "In-Person Meeting Proof",
                Message = "Meeting for your in-person meet start Proof Submitted.",
                Url = Url.Action("Conversation", "Messaging", new { exchangeId = model.ExchangeId })
            });

            TempData["SuccessMessage"] = "Your meeting end proof has been uploaded successfully.";
            return RedirectToAction("Conversation", "Messaging");
        }
        #endregion

        #region Enter End Meeting Details

        // GET: EnterEndMeetingDetails - Display the form for submitting end meeting proof and details.
        public async Task<IActionResult> EnterEndMeetingDetails(int exchangeId)
        {
            // First, try to locate the in-person meeting record. If not found, optionally create it.
            var meetingRecord = await _context.TblExchanges.FirstOrDefaultAsync(m => m.ExchangeId == exchangeId);
            if (meetingRecord == null)
            {
                return RedirectToAction("EP404", "EP");
            }

            // Build the view model. Default end meeting time is now.
            var viewModel = new EndMeetingDetailsVM
            {
                ExchangeId = exchangeId,
                EndMeetingDateTime = DateTime.UtcNow,
                EndProofLocation = string.Empty,
                EndMeetingNotes = string.Empty,
                CapturedProof = string.Empty
            };

            return View(viewModel);
        }

        // POST: EnterEndMeetingDetails - Process the end meeting details form.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnterEndMeetingDetails(EndMeetingDetailsVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                var proofRecord = await _context.TblInpersonMeetingProofs.FirstOrDefaultAsync(m => m.ExchangeId == model.ExchangeId);
                if (proofRecord == null)
                {
                    proofRecord = new TblInpersonMeetingProof
                    {
                        ExchangeId = model.ExchangeId,
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.TblInpersonMeetingProofs.Add(proofRecord);
                }

                // Save the captured end proof image using the helper method.
                proofRecord.EndProofImageUrl = SaveCapturedProof(model.CapturedProof);
                proofRecord.EndProofDateTime = model.EndMeetingDateTime;
                proofRecord.EndProofLocation = model.EndProofLocation;
                //meetingRecord.EndMeetingNotes = model.EndMeetingNotes;

                // update the parent exchange record’s flag if you are still using it.
                var exchangeRecord = await _context.TblExchanges.FirstOrDefaultAsync(e => e.ExchangeId == model.ExchangeId);
                if (exchangeRecord != null)
                {
                    exchangeRecord.IsMeetingEnded = true;
                }

                // Log the end meeting proof in the exchange history.
                int currentUserId = GetUserId();
                var historyRecord = new TblExchangeHistory
                {
                    ExchangeId = model.ExchangeId,
                    ActionType = "Meeting End Proof",
                    ChangedStatus = "Meeting Ended",
                    ChangedBy = currentUserId,
                    ChangeDate = DateTime.UtcNow,
                    Reason = $"End proof submitted at {DateTime.UtcNow:dd MMMM, yyyy HH:mm} from location: {model.EndProofLocation}. Notes: {model.EndMeetingNotes}"
                };
                _context.TblExchangeHistories.Add(historyRecord);

                await _context.SaveChangesAsync();

                // log notification:
                await _notif.AddAsync(new TblNotification
                {
                    UserId = GetUserId(),
                    Title = "In-Person Meeting Proof",
                    Message = "Meeting for your in-person meet end Proof Submitted.",
                    Url = Url.Action("Conversation", "Messaging", new { exchangeId = model.ExchangeId })
                });

                TempData["SuccessMessage"] = "Your end meeting details have been submitted successfully.";
                return RedirectToAction("Conversation", "Messaging", new { exchangeId = model.ExchangeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting end meeting details for exchange {ExchangeId}", model.ExchangeId);
                TempData["ErrorMessage"] = "An error occurred while submitting your end meeting details.";
                return View(model);
            }
        }

        #endregion

        #region Helper Class

        // Helper method: SaveCapturedProof
        private string SaveCapturedProof(string capturedProof)
        {
            // Remove any data URL prefix.
            int commaIndex = capturedProof.IndexOf(",");
            if (commaIndex >= 0)
            {
                capturedProof = capturedProof.Substring(commaIndex + 1);
            }

            byte[] imageBytes = Convert.FromBase64String(capturedProof);

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inpersonMeetingProof");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + ".png";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            System.IO.File.WriteAllBytes(filePath, imageBytes);

            return $"/uploads/inpersonMeetingProof/{uniqueFileName}";
        }

        // Helper method: GenerateOtp – creates a 6-digit OTP.
        private string GenerateOtp()
        {
            int otp = RandomNumberGenerator.GetInt32(100000, 1000000);
            return otp.ToString();
        }

        /// <summary>
        /// Retrieves the current user's ID from the claims.
        /// </summary>
        /// <returns>The user ID as an integer.</returns>
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogInformation("Current user ID from claims: {UserId}", userId);
                return userId;
            }
            throw new Exception("User ID not found in claims.");
        }
        #endregion

    }
}