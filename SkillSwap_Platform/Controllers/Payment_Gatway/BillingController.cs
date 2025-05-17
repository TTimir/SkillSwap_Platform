using System;
using System.Drawing;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway;
using SkillSwap_Platform.Services.Payment_Gatway;
using SkillSwap_Platform.Services.Payment_Gatway.RazorPay;

namespace SkillSwap_Platform.Controllers.Payment_Gatway
{
    public class BillingController : Controller
    {
        private readonly RazorpayService _rzp;
        private readonly ISubscriptionService _subs;
        private readonly IPaymentLogService _paymentLog;
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            RazorpayService rzp,
            IPaymentLogService payment,
            ISubscriptionService subs,
            SkillSwapDbContext db,
            ILogger<BillingController> logger)
        {
            _rzp = rzp;
            _paymentLog = payment;
            _subs = subs;
            _db = db;
            _logger = logger;
        }

        [HttpPost, Route("Billing/Callback")]
        public async Task<IActionResult> Callback([FromForm] CallbackModel model)
        {
            if (!_rzp.VerifySignature(
                  model.razorpay_order_id,
                  model.razorpay_payment_id,
                  model.razorpay_signature))
            {
                _logger.LogWarning("Invalid signature for payment {PaymentId}", model.razorpay_payment_id);
                return View("404", "EP");
            }

            // determine plan duration
            var now = DateTime.UtcNow;
            var end = model.planName switch
            {
                "Premium" => now.AddMonths(1),
                "Pro" => now.AddMonths(1),
                "Growth" => now.AddMonths(1),
                _ => now
            };

            // get current user
            var userId = GetUserId();
            if (userId == null)
            {
                _logger.LogError("Unauthenticated callback for payment {PaymentId}", model.razorpay_payment_id);
                return View("404", "EP");
            }

            await _subs.CreateAsync(userId ?? 0, model.planName, now, end);
            _logger.LogInformation("Subscription for user {UserId} upgraded to {Plan} until {EndDate}", userId, model.planName, end);

            return View("Success", model);
        }

        public class CallbackModel
        {
            public string razorpay_payment_id { get; set; }
            public string razorpay_order_id { get; set; }
            public string razorpay_signature { get; set; }
            public string planName { get; set; }  // add this to your callback_url query
        }

        // 1) Show the pricing grid
        [HttpGet]
        [Route("Billing/Pricing")]
        public IActionResult Pricing()
        {
            return View();
        }

        [HttpPost]
        [Route("Billing/Checkout")]
        public IActionResult Checkout([FromBody] PlanRequest req)
        {
            try
            {
                var order = _rzp.CreateOrderForPlan(req.Plan, req.BillingCycle);
                return Json(order);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid plan in Checkout: {Plan}", req.Plan);
                return BadRequest("Invalid plan");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Razorpay order");
                return StatusCode(500, "Could not create order");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelAutoRenew([FromForm] string reason)
        {
            var userId = GetUserId() ?? throw new Exception("Not signed in");
            if (string.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError(nameof(reason), "Please provide a reason");
                return RedirectToAction("Index", "UserDashboard");
            }

            await _subs.CancelAutoRenewAsync(userId, reason);
            TempData["Success"] = "Your subscription will not auto-renew.  Thanks for the feedback!";
            return RedirectToAction("Index", "UserDashboard");
        }

        // 3) Verify signature after payment
        public class PaymentResponse
        {
            public string razorpay_payment_id { get; set; }
            public string razorpay_order_id { get; set; }
            public string razorpay_signature { get; set; }
            public string planName { get; set; }
            public string billingCycle { get; set; }
        }

        [HttpPost]
        [Route("Billing/Verify")]
        public async Task<IActionResult> Verify([FromBody] PaymentResponse resp)
        {
            var ok = _rzp.VerifySignature(
                resp.razorpay_order_id,
                resp.razorpay_payment_id,
                resp.razorpay_signature
            );
            if (!ok)
                return Json(new { success = false, message = "Payment verification failed." });

            // signature is valid → update the DB
            var userId = GetUserId() ?? throw new Exception("Unauthenticated");
            if (await _paymentLog.HasProcessedAsync(resp.razorpay_order_id))
                return Json(new { success = true, message = "Already processed" });

            var start = DateTime.UtcNow;
            var end = resp.billingCycle == "yearly"
                ? start.AddYears(1)
                : start.AddMonths(1);

            _subs.UpsertAsync(userId, resp.planName, resp.billingCycle, start, end);

            // TODO: update user subscription, unlock features...
            return Json(new { success = true, message = "Payment successful! Subscription updated." });
        }

        [HttpGet, Route("Billing/BillingHistory")]
        public async Task<IActionResult> BillingHistory(int page = 1, int pageSize = 10)
        {
            var userId = GetUserId();
            if (userId == null)
                return RedirectToAction("Pricing");

            // 1) load just the subscriptions (no cancellations yet)
            var allSubs = await _db.Subscriptions
                                   .Where(s => s.UserId == userId)
                                   .OrderByDescending(s => s.StartDate)
                                   .AsNoTracking()
                                   .ToListAsync();

            // 2) total count for your pager
            var total = allSubs.Count;

            // 3) slice out this page
            var subsPage = allSubs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 4) *all* cancels for this user (should be small)
            var allCancels = await _db.CancellationRequests
                .Where(c => subsPage.Select(s => s.UserId).Contains(userId.Value))
                .ToListAsync();

            // 5) now filter in‐memory by subscription id
            var items = subsPage.Select(s =>
            {
                var cr = allCancels.FirstOrDefault(c => c.SubscriptionId == s.Id);
                return new SubscriptionHistoryItem
                {
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    PlanName = s.PlanName,
                    BillingCycle = s.BillingCycle,
                    IsAutoRenew = s.IsAutoRenew,
                    CancelReason = cr?.Reason,
                    CancelledAt = cr?.RequestedAt
                };
            }).ToList();

            // 6) build and return VM
            var vm = new BillingHistoryVM
            {
                BillingHistory = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };

            return View(vm);
        }

        private int? GetUserId()
        {
            // First, try to get from session (set during registration/OTP)
            int? tempUserId = HttpContext.Session.GetInt32("TempUserId");
            if (tempUserId != null)
                return tempUserId;
            // Fallback: if user is signed in, try claims.
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int userId))
                return userId;
            return null;
        }
    }
}