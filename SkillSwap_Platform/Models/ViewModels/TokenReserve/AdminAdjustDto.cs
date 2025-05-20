namespace SkillSwap_Platform.Models.ViewModels.TokenReserve
{
    public class AdminAdjustDto
    {
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; }    // “Refund”/“Correction”/“Promo”/“Other”
        public string Reason { get; set; }
    }
}
