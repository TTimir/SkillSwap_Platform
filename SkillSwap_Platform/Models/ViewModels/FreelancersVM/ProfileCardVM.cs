﻿using SkillSwap_Platform.Models.ViewModels.UserProfileMV;

namespace SkillSwap_Platform.Models.ViewModels.FreelancersVM
{
    public class ProfileCardVM
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Designation { get; set; }
        public string ProfileImage { get; set; }
        public string Skills { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public decimal Rating { get; set; }
        public List<string> OfferedSkillAreas { get; set; } = new List<string>();
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public double? Recommendation { get; set; }
        public double? JobSuccessRate { get; set; }
        public DateTime? LastActive { get; set; }
        public bool IsVerified { get; set; }
        public List<BadgeAwardVM> Badges { get; set; } = new List<BadgeAwardVM>();
    }
}
