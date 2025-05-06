namespace SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel
{
    public class OfferSearchResultDto
    {
        public int OfferId { get; set; }
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public string OwnerUserName { get; set; } = "";
        public int TotalFlags { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
