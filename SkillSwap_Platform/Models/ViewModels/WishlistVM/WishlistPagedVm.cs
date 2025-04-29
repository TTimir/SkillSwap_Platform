namespace SkillSwap_Platform.Models.ViewModels.WishlistVM
{
    public class WishlistPagedVm
    {
        public List<OfferWishlistVm> Items { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
