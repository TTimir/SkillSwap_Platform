using Microsoft.EntityFrameworkCore;

namespace SkillSwap_Platform.Models.ViewModels.BadgeTire
{
    public class BadgeDefinition
    {
        public int BadgeId { get; }
        public string Name { get; }
        public string Description { get; }
        public BadgeTier Tier { get; }
        public Func<SkillSwapDbContext, int, bool> Criteria { get; }
         public string IconPath  { get; }

        public BadgeDefinition(
            int badgeId,
            string name,
            string description,
            BadgeTier tier,
            Func<SkillSwapDbContext, int, bool> criteria,
            string iconPath)
        {
            BadgeId = badgeId;
            Name = name;
            Description = description;
            Tier = tier;
            Criteria = criteria;
            IconPath = iconPath;
        }

        public enum BadgeTier
        {
            Common,
            Uncommon,
            Rare,
            Epic,
            Legendary,
            Mythic
        }

    }
}
