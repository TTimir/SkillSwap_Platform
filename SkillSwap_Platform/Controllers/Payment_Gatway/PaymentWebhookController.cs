using Microsoft.AspNetCore.Mvc;
using SkillSwap_Platform.Services.Payment_Gatway.RazorPay;
using SkillSwap_Platform.Services.Payment_Gatway;
using System.Text.Json;

namespace SkillSwap_Platform.Controllers.Payment_Gatway
{
    [Route("api/payment/webhook")]
    [ApiController]
    public class PaymentWebhookController : Controller
    {
        private readonly IRazorpayService _rzp;
        private readonly ISubscriptionService _subsSvc;
        private readonly ILogger<PaymentWebhookController> _logger;
        public PaymentWebhookController(
        IRazorpayService rzp,
        ISubscriptionService subsSvc,
        ILogger<PaymentWebhookController> logger)
        {
            _rzp = rzp;
            _subsSvc = subsSvc;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> HandleRazorpayWebhook()
        {
            // 1) Grab raw body and signature header
            var payload = await new StreamReader(Request.Body).ReadToEndAsync();
            var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();

            // 2) Verify signature
            if (!_rzp.VerifyWebhookSignature(payload, signature))
            {
                _logger.LogWarning("Invalid webhook signature");
                return BadRequest();
            }

            // 3) Parse event
            var json = JsonDocument.Parse(payload);
            var evt = json.RootElement.GetProperty("event").GetString();
            if (evt != "payment.captured")
                return Ok();  // we only care about successful payments

            var payloadObj = json.RootElement
                .GetProperty("payload")
                .GetProperty("payment")
                .GetProperty("entity");

            var orderId = payloadObj.GetProperty("order_id").GetString()!;
            var paymentId = payloadObj.GetProperty("id").GetString()!;
            var plan = payloadObj.GetProperty("planName").GetString()!;
            var cycle = payloadObj.GetProperty("billingCycle").GetString()!;
            // Razorpay amounts come in paise: divide by 100 for INR
            var amount = payloadObj.GetProperty("amount").GetDecimal() / 100m;

            // 4) Update your subscription record
            try
            {
                await _subsSvc.RecordPaymentAsync(
                    orderId,
                    paymentId,
                    amount,
                    plan,
                    cycle
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording payment for order {OrderId}", orderId);
                return StatusCode(500);
            }

            return Ok();
        }
    }
}
