namespace SkillSwap_Platform.Models.ViewModels.TokenStatementVM
{
    public class TokenStatementVM
    {
        public decimal NetTokensReceived { get; set; }
        public decimal TokensSpent { get; set; }
        public decimal TotalHeld { get; set; }
        public decimal AvailableForWithdrawal { get; set; }

        public decimal FutureReceivedCount { get; set; }

        public List<TokenTransactionVm> Transactions { get; set; }

        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        public int TotalPages
            => (int)Math.Ceiling((double)TotalCount / PageSize);
        public int FirstItemOnPage => (CurrentPage - 1) * PageSize + 1;
        public int LastItemOnPage => Math.Min(CurrentPage * PageSize, TotalCount);
    }

    public class TokenTransactionVm
    {
        public DateTime Date { get; set; }
        public string Type { get; set; }           // e.g. "Hold", "Release", "Topup", "Spend"
        public string Detail { get; set; }         // description
        public decimal Amount { get; set; }        // total tokens moved
        public bool Highlight { get; set; }        // e.g. true for “active” rows
        public int CounterpartyId { get; set; }
        public string CounterpartyName { get; set; }
    }
}
