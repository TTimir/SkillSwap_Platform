using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Razorpay.Api;
using SkillSwap_Platform.Models.ViewModels;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway.POCO;

namespace SkillSwap_Platform.Services.Payment_Gatway.RazorPay
{
    public class RazorpayService : IRazorpayService
    {
        private readonly RazorpaySettings _rzpSettings;
        private readonly PlanSettings _planSettings;
        public RazorpayService(
            IOptions<RazorpaySettings> rzpOpts,
            IOptions<PlanSettings> planOpts)
        {
            _rzpSettings = rzpOpts.Value;
            _planSettings = planOpts.Value;
        }


        // Expose the test/live key for your frontend
        public string Key => _rzpSettings.Key;


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
                var client = new RazorpayClient(_rzpSettings.Key, _rzpSettings.Secret);
                var order = await Task.Run(() => client.Order.Create(new Dictionary<string, object>
                {
                    ["amount"] = amountInPaise,
                    ["currency"] = "INR",
                    ["receipt"] = receipt,
                    ["payment_capture"] = 1
                }));

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
    }
}