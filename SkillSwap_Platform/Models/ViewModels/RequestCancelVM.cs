using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace SkillSwap_Platform.Models.ViewModels
{
    public class RequestCancelVM
    {
        [HiddenInput]
        public int ExchangeId { get; set; }

        [Required, StringLength(500)]
        [Display(Name = "Why are you cancelling?")]
        public string Reason { get; set; } = "";
    }
}
