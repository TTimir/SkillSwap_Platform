namespace SkillSwap_Platform.Models.ViewModels.WishlistVM
{
    public class OfferWishlistVm
    {
        public int OfferId { get; set; }
        public string Title { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Category { get; set; } = "";
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public string OwnerUsername { get; set; } = "";
        public string OwnerProfileImage { get; set; } = "";
        public string Location { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string DetailUrl { get; set; } = "";
        public string RemoveUrl { get; set; } = "";
    }

}
