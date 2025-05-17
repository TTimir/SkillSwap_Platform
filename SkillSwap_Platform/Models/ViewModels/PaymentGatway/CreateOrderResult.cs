namespace SkillSwap_Platform.Models.ViewModels.PaymentGatway
{
    public class CreateOrderResult
    {
        public string Key { get; set; }
        public string OrderId { get; set; }
        public int Amount { get; set; }   // in paise
        public string Currency { get; set; }
        public string PlanName { get; set; }
        public decimal PlanPrice { get; set; }   // just for UI if you need it
        public string BillingCycle { get; set; }   
    }
}
