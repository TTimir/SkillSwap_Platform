﻿using SkillSwap_Platform.Services.AdminControls.AdminSearch.ViewModel;

namespace SkillSwap_Platform.Models.ViewModels.AdminControl.AdminSearch
{
    public class UserDetailVM
    {
        public UserDetailDto User { get; }
        public UserDetailVM(UserDetailDto dto) => User = dto;
    }
}
