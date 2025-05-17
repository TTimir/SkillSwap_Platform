namespace SkillSwap_Platform.Models.ViewModels.PaymentGatway
{
    public class CallbackModel
    {
        public string razorpay_payment_id { get; set; }
        public string razorpay_order_id { get; set; }
        public string razorpay_signature { get; set; }
        public string planName { get; set; }  // add this to your callback_url query
    }
}
