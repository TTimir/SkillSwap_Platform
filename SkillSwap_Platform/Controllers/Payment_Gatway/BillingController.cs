﻿using System;
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

            try
            {
                var result = await _rzp.CreateOrderAsync(req.Plan, req.BillingCycle);
                return Json(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid plan in Checkout: {Plan}", req.Plan);
                return BadRequest("Invalid plan");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Razorpay order");
                return StatusCode(500, $"Order creation failed: {ex.Message}");
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
        [HttpPost("Verify")]
        public async Task<IActionResult> Verify([FromBody] PaymentResponse resp)
        {
            var ok = _rzp.VerifySignature(
                resp.razorpay_order_id,
                resp.razorpay_payment_id,
                resp.razorpay_signature
            );
            if (!ok)
                return Json(new { success = false, message = "Payment verification failed." });

            if (await _paymentLog.HasProcessedAsync(resp.razorpay_order_id))
                return Ok(new { success = true, message = "Already processed" });

            // signature is valid → update the DB
            var userId = GetUserId() ?? throw new Exception("Unauthenticated");
            var now = DateTime.UtcNow;

            var activeSub = await _subs.GetActiveAsync(userId);
            DateTime newStart, newEnd;

            // are we renewing an unexpired subscription of the same plan+cycle?
            if (activeSub != null
                && activeSub.PlanName.Equals(resp.planName, StringComparison.OrdinalIgnoreCase)
                && activeSub.BillingCycle.Equals(resp.billingCycle, StringComparison.OrdinalIgnoreCase)
                && activeSub.EndDate > now)
            {
                // extend from the *current* end date
                newStart = activeSub.StartDate;          // you could also leave StartDate untouched
                newEnd = activeSub.EndDate
                             .AddMonths(resp.billingCycle == "yearly" ? 12 : 1);
            }
            else
            {
                // brand-new or expired → start today
                newStart = now;
                newEnd = now.AddYears(resp.billingCycle == "yearly" ? 1 : 0)
                              .AddMonths(resp.billingCycle == "yearly" ? 0 : 1);
            }
            try
            {

                var sub = await _subs.GetActiveAsync(userId)
                            ?? throw new InvalidOperationException("No subscription to attach order to");
                sub.GatewayOrderId = resp.razorpay_order_id;
                await _db.SaveChangesAsync();

                // 6) Fetch the paid amount from Razorpay API
                var client = new RazorpayClient(_rzp.Key, _rzp.Secret);
                var payment = await Task.Run(() => client.Payment.Fetch(resp.razorpay_payment_id));
                var amountInPaise = Convert.ToDecimal(payment["amount"]);
                var paidAmount = amountInPaise / 100m;

                // 7) Record payment + fire notifications
                await _subs.RecordPaymentAsync(
                    resp.razorpay_order_id,   // orderId
                    resp.razorpay_payment_id, // paymentId
                    paidAmount,               // paidAmount
                    resp.planName,            // desiredPlanName
                    resp.billingCycle         // billingCycle
                );

                _logger.LogInformation("Payment {OrderId} applied to user {UserId}", resp.razorpay_order_id, userId);

                // TODO: update user subscription, unlock features...
                return Json(new { success = true, message = "Payment successful! Subscription updated." });
            }
            catch (Exception ex)
            {
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