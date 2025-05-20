namespace SkillSwap_Platform.Models.ViewModels.PaymentGatway
{
    public class PricingViewModel
    {
        public string CurrentPlan { get; set; }
        public string CurrentCycle { get; set; }   // "monthly" or "yearly"
        public DateTime? CurrentEndDate { get; set; }  // null for Free
    }
}
