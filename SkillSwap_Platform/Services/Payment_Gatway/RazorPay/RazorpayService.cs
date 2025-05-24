using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Razorpay.Api;
using SkillSwap_Platform.Controllers.Payment_Gatway;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway.POCO;

namespace SkillSwap_Platform.Services.Payment_Gatway.RazorPay
{
    public class RazorpayService : IRazorpayService
    {
        private readonly RazorpaySettings _rzpSettings;
        private readonly PlanSettings _planSettings;
        private readonly ILogger<RazorpayService> _logger;

        public RazorpayService(
            IOptions<RazorpaySettings> rzpOpts,
            IOptions<PlanSettings> planOpts,
            ILogger<RazorpayService> logger)
        {
            _rzpSettings = rzpOpts.Value;
            _planSettings = planOpts.Value;
            _logger = logger;
        }


        // Expose the test/live key for your frontend
        public string Key => _rzpSettings.Key;
        public string Secret => _rzpSettings.Secret;

        public async Task<CreateOrderResult> CreateOrderAsync(string planName, string billingCycle)
        {
            // 1) look up the plan in config
            var plan = _planSettings.Plans
                .SingleOrDefault(p => p.Name.Equals(planName, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Unknown plan “{planName}”", nameof(planName));

            // 2) compute price
            var basePrice = plan.MonthlyPrice;
            var isYearly = billingCycle.Equals("yearly", StringComparison.OrdinalIgnoreCase);
            var multiplier = isYearly
                                       ? 12m * (1 - _planSettings.Discount)
                                       : 1m;
            var priceInRupees = Math.Round(basePrice * multiplier, 0);

            // 3) paise
            var amountInPaise = (int)(priceInRupees * 100);

            // generate a receipt and actually create the Razorpay order
            var receipt = $"rcpt_{Guid.NewGuid():N}";
            try
            {
                var client = new RazorpayClient(Key, Secret);
                var parameters = new Dictionary<string, object>
                {
                    ["amount"] = amountInPaise,
                    ["currency"] = "INR",
                    ["receipt"] = receipt,
                    ["payment_capture"] = 1
                };
                Order order = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        order = client.Order.Create(parameters);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Razorpay order creation attempt {Attempt} failed", attempt);
                        // Log the exception including any inner WebException info
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                    }
                }
                if (order == null)
                    throw new InvalidOperationException($"Unable to create Razorpay order after 3 attempts");

                return new CreateOrderResult
                {
                    Key = Key,
                    OrderId = order["id"].ToString(),
                    Amount = amountInPaise,
                    Currency = order["currency"].ToString(),
                    PlanName = planName,
                    PlanPrice = priceInRupees,
                    BillingCycle = billingCycle
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Razorpay order creation failed", ex);
            }
        }

        /// <summary>
        /// Manually verify the Razorpay signature per the docs:
        /// generated_signature = HMAC_SHA256(orderId + "|" + paymentId, secret)
        /// </summary>
        public bool VerifySignature(string orderId, string paymentId, string signature)
        {
            // 0) Guard: no signature → fail fast
            if (string.IsNullOrEmpty(signature))
                return false;

            // 1) Build the payload exactly as Razorpay expects:
            var payload = $"{orderId}|{paymentId}";

            // 2) Compute the HMAC-SHA256 digest
            var keyBytes = Encoding.UTF8.GetBytes(_rzpSettings.Secret);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            byte[] computedHash;
            using (var hmac = new HMACSHA256(keyBytes))
            {
                computedHash = hmac.ComputeHash(payloadBytes);
            }

            // 3) Decode the signature (hex → bytes).  
            //    This handles uppercase, lowercase, etc.
            try
            {
                var signatureBytes = Enumerable.Range(0, signature.Length / 2)
                    .Select(i => Convert.ToByte(signature.Substring(i * 2, 2), 16))
                    .ToArray();

                // 4) Constant-time compare
                return CryptographicOperations.FixedTimeEquals(computedHash, signatureBytes);
            }
            catch
            {
                // invalid hex string
                return false;
            }
        }

        // Services/Payment_Gatway/RazorPay/RazorpayService.cs
        public bool VerifyWebhookSignature(string payload, string signature)
        {
            if (string.IsNullOrEmpty(signature)) return false;

            var keyBytes = Encoding.UTF8.GetBytes(_rzpSettings.WebhookSecret);
            using var hmac = new HMACSHA256(keyBytes);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            try
            {
                // signature is hex; convert to bytes
                var sigBytes = Enumerable.Range(0, signature.Length / 2)
                    .Select(i => Convert.ToByte(signature.Substring(i * 2, 2), 16))
                    .ToArray();
                return CryptographicOperations.FixedTimeEquals(computedHash, sigBytes);
            }
            catch
            {
                return false;
            }
        }

    }
}