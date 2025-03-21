using System.ComponentModel.DataAnnotations.Schema;

namespace SkillSwap_Platform.Models.ViewModels.ExchangeVM
{
    public class OfferDetailsVM
    {
        public int OfferId { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string SkillName { get; set; }
        public int TimeCommitmentDays { get; set; }
        public int OfferOwnersSkill { get; set; }
        [ForeignKey("SkillIdOfferOwner")]
        public TblSkill Skill { get; set; }
        // List of image URLs from TblOfferPortfolio
        public List<string> PortfolioImages { get; set; } = new List<string>();
        public List<TblOffer> Offers { get; set; } = new List<TblOffer>();

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

    }
}
