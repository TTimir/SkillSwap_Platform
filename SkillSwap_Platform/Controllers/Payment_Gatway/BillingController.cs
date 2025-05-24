using System;
using System.Drawing;
using System.Security.Claims;
using Hangfire.Logging;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway;
using SkillSwap_Platform.Services.Payment_Gatway;
using SkillSwap_Platform.Services.Payment_Gatway.RazorPay;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace SkillSwap_Platform.Controllers.Payment_Gatway
{
    [Route("Billing")]
    public class BillingController : Controller
    {
        private readonly IRazorpayService _rzp;
        private readonly ISubscriptionService _subs;
        private readonly IPaymentLogService _paymentLog;
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            IRazorpayService rzp,
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

        [HttpPost, Route("Callback")]
        public async Task<IActionResult> Callback([FromForm] CallbackModel model)
        {
            if (!_rzp.VerifySignature(
                  model.razorpay_order_id,
                  model.razorpay_payment_id,
                  model.razorpay_signature))
            {
                _logger.LogWarning("Invalid signature for payment {PaymentId}", model.razorpay_payment_id);
                return View("EP404", "EP");
            }

            // determine plan duration
            var now = DateTime.UtcNow;
            var end = model.billingCycle.Equals("yearly", StringComparison.OrdinalIgnoreCase)
              ? now.AddYears(1)
              : now.AddMonths(1);

            // get current user
            var userId = GetUserId();
            if (userId == null)
            {
                _logger.LogError("Unauthenticated callback for payment {PaymentId}", model.razorpay_payment_id);
                return View("404", "EP");
            }

            // 5) Stamp the GatewayOrderId so RecordPaymentAsync can find it
            var sub = await _subs.GetActiveAsync(userId.Value);
            sub.GatewayOrderId = model.razorpay_order_id;
            await _db.SaveChangesAsync();

            // 6) Determine paid amount
            decimal paidAmount;
            if (model.razorpay_amount > 0)
            {
                // if your form posts amount in paise:
                paidAmount = model.razorpay_amount / 100m;
            }
            else
            {
                // otherwise fetch from Razorpay API:
                var client = new RazorpayClient(_rzp.Key, _rzp.Secret);
                var payment = await Task.Run(() => client.Payment.Fetch(model.razorpay_payment_id));
                paidAmount = Convert.ToDecimal(payment["amount"]) / 100m;
            }

            // 7) Record payment details + fire notifications
            await _subs.RecordPaymentAsync(
                model.razorpay_order_id,    // orderId
                model.razorpay_payment_id,  // paymentId
                paidAmount,                 // paidAmount
                model.planName,             // desiredPlanName
                model.billingCycle          // billingCycle ("monthly" or "yearly")
            );

            _logger.LogInformation(
                "User {UserId} subscribed to {Plan} (order {OrderId}) paid {Amount}",
                userId, model.planName, model.razorpay_order_id, paidAmount
            );

            return View("Success", model);
        }


        // 1) Show the pricing grid
        [HttpGet]
        [Route("Pricing")]
        public async Task<IActionResult> Pricing()
        {
            var userId = GetUserId();
            Models.Subscription activeSub = null;
            if (userId.HasValue)
                activeSub = await _subs.GetActiveAsync(userId.Value);

            var vm = new PricingViewModel
            {
                CurrentPlan = activeSub?.PlanName ?? "Free",
                CurrentCycle = activeSub?.BillingCycle ?? "monthly",
                CurrentEndDate = activeSub?.EndDate
            };
            return View(vm);
        }

        [HttpPost("Checkout")]
        public async Task<IActionResult> Checkout([FromBody] PlanRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(req.BillingCycle) ||
            !(req.BillingCycle.Equals("monthly", StringComparison.OrdinalIgnoreCase)
              || req.BillingCycle.Equals("yearly", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest("Invalid billing cycle");
            }

            try
            {
                var result = await _rzp.CreateOrderAsync(req.Plan, req.BillingCycle);
                return Json(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Checkout failed: invalid plan or cycle ({Plan}, {Cycle})",
                    req.Plan, req.BillingCycle);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout: unexpected error for plan {Plan}, cycle {Cycle}",
                    req.Plan, req.BillingCycle);
                return StatusCode(500, "Unable to create payment order. Please try again later.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelAutoRenew()
        {
            var userId = GetUserId() ?? throw new Exception("Not signed in");
            var user = await _db.TblUsers.FindAsync(userId)
                          ?? throw new Exception("User not found");

            var timestamp = DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var reason = $"Cancelled by \"{user.UserName}\" at \"{timestamp}\".";

            await _subs.CancelAutoRenewAsync(userId, reason);
            TempData["Success"] = "Your subscription will not auto-renew. Thanks for being a valuable swapper";
            return RedirectToAction("Index", "UserDashboard");
        }

        // 3) Verify signature after payment
        [HttpPost("Verify")]
        public async Task<IActionResult> Verify([FromBody] PaymentResponse resp)
        {
            var ok = _rzp.VerifySignature(
                resp.razorpay_order_id,
                resp.razorpay_payment_id,
                resp.razorpay_signature
            );
            if (!ok)
                return Json(new { success = false, message = "Invalid payment signature." });

            if (await _paymentLog.HasProcessedAsync(resp.razorpay_order_id))
                return Ok(new { success = true, message = "Already processed" });

            decimal paidAmount;
            try
            {
                var client = new RazorpayClient(_rzp.Key, _rzp.Secret);
                var payment = await Task.Run(() => client.Payment.Fetch(resp.razorpay_payment_id));
                paidAmount = Convert.ToDecimal(payment["amount"]) / 100m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch payment details for {PaymentId}", resp.razorpay_payment_id);
                return StatusCode(500, new { success = false, message = "Unable to confirm payment amount." });
            }

            // signature is valid → update the DB
            var userId = GetUserId();
            if (userId == null)
                return Json(new { success = false, message = "User not authenticated." });

            var now = DateTime.UtcNow;
            var start = now;
            var end = resp.billingCycle.Equals("yearly", StringComparison.OrdinalIgnoreCase)
                        ? now.AddYears(1)
                        : now.AddMonths(1);

            using var tx = await _db.Database.BeginTransactionAsync().ConfigureAwait(false); ;
            try
            {
                await _subs.UpsertAsync(
                    userId.Value,
                    resp.planName,
                    resp.billingCycle,
                    start,
                    end,
                    resp.razorpay_order_id,
                    resp.razorpay_payment_id,
                    paidAmount,
                    sendEmail: true
                ).ConfigureAwait(false); ;

                await _paymentLog.LogAsync(userId.Value, resp.razorpay_order_id, resp.razorpay_payment_id)
                         .ConfigureAwait(false);

                await tx.CommitAsync().ConfigureAwait(false);
                // TODO: update user subscription, unlock features...
                return Json(new { success = true, message = "Payment successful! Subscription updated." });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                _logger.LogError(ex, "Error processing payment {OrderId}", resp.razorpay_order_id);
                return StatusCode(500, new { success = false, message = "Processing failed" });
            }
        }

        [HttpGet, Route("BillingHistory")]
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
            if (HttpContext.Session.TryGetValue("TempUserId", out var data)
                && BitConverter.ToInt32(data) is int tempId)
                return tempId;

            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : (int?)null;
        }
    }
}