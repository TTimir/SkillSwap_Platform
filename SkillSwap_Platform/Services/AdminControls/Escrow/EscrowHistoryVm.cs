namespace SkillSwap_Platform.Services.AdminControls.Escrow
{
    public class EscrowHistoryVm
    {
        public int TransactionId { get; set; }
        public int ExchangeId { get; set; }
        public string FromUserName { get; set; } = "";
        public string ToUserName { get; set; } = "";
        public decimal Amount { get; set; }
        public string TxType { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; } = "";
        public bool IsReleased { get; set; }
    }
}
