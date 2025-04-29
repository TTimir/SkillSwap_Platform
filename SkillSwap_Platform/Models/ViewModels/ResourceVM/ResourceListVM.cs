namespace SkillSwap_Platform.Models.ViewModels.ResourceVM
{
    public class ResourceListVM
    {
        public int ExchangeId { get; set; }
        public int OfferId { get; set; }
        public List<TblResource> Resources { get; set; }
        public int CurrentUserId { get; set; }
        public List<TblResource> MyResources { get; set; }
        public List<TblResource> ReceivedResources { get; set; }

        public int CurrentPage { get; set; }
        public int PageSize { get; set; }

        public int MyResourcesTotalPages { get; set; }
        public int ReceivedResourcesTotalPages { get; set; }
    }
}