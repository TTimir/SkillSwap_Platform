namespace SkillSwap_Platform.Models.ViewModels.PaymentGatway.POCO
{
    public class PlanDefinition
    {
        public string Name { get; set; } = null!;
        public decimal MonthlyPrice { get; set; }
    }

    public class PlanSettings
    {
        /// <summary>
        /// Fractional discount for yearly billing (e.g. 0.196 for 19.6% off).
        /// </summary>
        public decimal Discount { get; set; }

        /// <summary>
        /// All of your plans, with their base monthly prices.
        /// </summary>
        public List<PlanDefinition> Plans { get; set; } = new();
    }
}
