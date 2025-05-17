using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Razorpay.Api;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway;

namespace SkillSwap_Platform.Services.Payment_Gatway.RazorPay
{
    public class RazorpaySettings
    {
        public string Key { get; set; }
        public string Secret { get; set; }
    }

    public class PlanRequest
    {
        public string Plan { get; set; }
        public string BillingCycle { get; set; }  // "monthly" or "yearly"
    }

    public class PaymentResponse
    {
        public string razorpay_payment_id { get; set; }
        public string razorpay_order_id { get; set; }
        public string razorpay_signature { get; set; }
        public string PlanName { get; set; }
        public string BillingCycle { get; set; }
    }

    public class RazorpayService
    {
        private readonly RazorpaySettings _settings;
        public RazorpayService(IOptions<RazorpaySettings> opts)
            => _settings = opts.Value;

        // Expose the test/live key for your frontend
        public string Key => _settings.Key;

        // Single RazorpayClient instance
        private RazorpayClient Client
            => new RazorpayClient(_settings.Key, _settings.Secret);

        public object CreateOrderForPlan(string planName, string billingCycle)
        {
            // 1) determine base price
            decimal basePrice = planName switch
            {
                "Premium" => 190m,
                "Pro" => 490m,
                "Growth" => 990m,
                _ => throw new ArgumentException("Invalid plan", nameof(planName))
            };

            // 2) adjust for yearly
            var isYearly = billingCycle == "yearly";
            var multiplier = isYearly ? 12m * (1 - 0.196m) : 1m;
            var priceInRupees = Math.Round(basePrice * multiplier, 0);

            // 3) paise
            var amountInPaise = (int)(priceInRupees * 100);

            // generate a receipt and actually create the Razorpay order
            var receipt = $"rcpt_{Guid.NewGuid():N}";
            try
            {
                var order = Client.Order.Create(new Dictionary<string, object>
                {
                    ["amount"] = amountInPaise,
                    ["currency"] = "INR",
                    ["receipt"] = receipt,
                    ["payment_capture"] = 1
                });

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
                throw;  // let controller catch & return 500
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
            byte[] keyBytes = Encoding.UTF8.GetBytes(_settings.Secret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            byte[] computedHash;
            using (var hmac = new HMACSHA256(keyBytes))
            {
                computedHash = hmac.ComputeHash(payloadBytes);
            }

            // 3) Decode the signature (hex → bytes).  
            //    This handles uppercase, lowercase, etc.
            byte[] signatureBytes;
            try
            {
                signatureBytes = Enumerable
                    .Range(0, signature.Length)
                    .Where(i => i % 2 == 0)
                    .Select(i => Convert.ToByte(signature.Substring(i, 2), 16))
                    .ToArray();
            }
            catch
            {
                // invalid hex string
                return false;
            }

            // 4) Constant-time compare
            return CryptographicOperations.FixedTimeEquals(computedHash, signatureBytes);
        }
    }
}