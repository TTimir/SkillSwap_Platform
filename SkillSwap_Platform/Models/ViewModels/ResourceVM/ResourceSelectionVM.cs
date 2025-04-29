namespace SkillSwap_Platform.Models.ViewModels.ResourceVM
{
    public class ResourceSelectionVM
    {
        // The current user’s list of relevant offers (or exchanges) to choose from.
        public List<OfferOption> OfferOptions { get; set; }

        public int CurrentUserId { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalOffers { get; set; } // Total number of offers
        public int TotalPages { get; set; }
    }

    public class OfferOption
    {
        public int OfferId { get; set; }
        public int ExchangeId { get; set; }
        public string OfferTitle { get; set; }
        public string OfferImageUrl { get; set; }
        public decimal? TokenCost { get; set; }
        public int ReceivedCount { get; set; }
        public string Status { get; set; }

        public DateTime InitiatedDate { get; set; }
        public string ModeOfLearning { get; set; }
        public string Category { get; set; }
    }
}
