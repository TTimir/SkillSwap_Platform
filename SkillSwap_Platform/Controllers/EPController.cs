using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SkillSwap_Platform.Controllers
{
    [AllowAnonymous]
    public class EPController : Controller
    {
        // GET: EP
        public ActionResult EP404()
        {
            return View();
        }

        public ActionResult EP500()
        {
            return View();
        }

        public ActionResult EP600()
        {
            return View();
        }
    }
}
