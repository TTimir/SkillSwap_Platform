namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class OfferDeleteVM
    {
        public int OfferId { get; set; }
        public string Title { get; set; }
        public decimal TokenCost { get; set; }
        public int TimeCommitmentDays { get; set; }
        public string Category { get; set; }
        public string FreelanceType { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        // This property will hold the JSON string of portfolio URLs,
        // but for display we only need the first image.
        public string Portfolio { get; set; }
        public string ThumbnailUrl { get; set; }
    }
    public class OfferRestoreListVM
    {
        public List<OfferDeleteVM> DeletedOffers { get; set; } = new List<OfferDeleteVM>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

}
