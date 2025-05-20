namespace SkillSwap_Platform.Models.ViewModels
{
    public class RazorpaySettings
    {
        public string Key { get; set; } = default!;
        public string Secret { get; set; } = default!;
        public string WebhookSecret { get; set; } = default!;
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
        public string planName { get; set; }
        public string billingCycle { get; set; }
    }

}
