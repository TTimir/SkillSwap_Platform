using Microsoft.AspNetCore.Authorization;
using SkillSwap_Platform.Models.ViewModels;

namespace SkillSwap_Platform.Services.Payment_Gatway
{
    public class MinimumTierRequirement : IAuthorizationRequirement
    {
        public SubscriptionTier RequiredTier { get; }
        public MinimumTierRequirement(SubscriptionTier tier)
            => RequiredTier = tier;
    }
}
