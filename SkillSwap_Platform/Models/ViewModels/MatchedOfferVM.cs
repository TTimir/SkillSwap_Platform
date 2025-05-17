namespace SkillSwap_Platform.Models.ViewModels
{
    public class MatchedOfferVM
    {
        public int OfferId { get; set; }
        public string Title { get; set; }
        public string OwnerUserName { get; set; }

        // Skills _they_ offer that _you_ want
        public IReadOnlyList<string> SkillsOfferToMe { get; set; }

        // Skills _you_ offer that _they_ want
        public IReadOnlyList<string> SkillsIOfferToThem { get; set; }
    }
}
