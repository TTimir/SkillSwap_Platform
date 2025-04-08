namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class ExchangeDetailsVM
    {
        public TblExchange Exchange { get; set; }
        // The offer details are available through Exchange.Offer,
        // but you can also expose it separately if you prefer.
        public TblOffer Offer => Exchange.Offer;
        public TblContract Contract { get; set; }
        public IEnumerable<TblExchangeHistory> PagedHistory { get; set; }
        public int TimelineCurrentPage { get; set; }
        public int TimelineTotalPages { get; set; }
    }
}
