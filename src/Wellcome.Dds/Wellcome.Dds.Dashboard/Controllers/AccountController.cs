using Microsoft.AspNetCore.Mvc;
using Utils;
using Wellcome.Dds.AssetDomain.DigitalObjects;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class AccountController : Controller
    {
        private readonly IDigitalObjectRepository digitalObjectRepository;

        public AccountController(IDigitalObjectRepository digitalObjectRepository)
        {
            this.digitalObjectRepository = digitalObjectRepository;
        }

        public ActionResult UserPage(string id = null)
        {
            var username = id.HasText() ? id : User.Identity.Name;
            var recentActions = digitalObjectRepository.GetRecentActions(200, username);
            ViewBag.Subject = username;
            return View(recentActions);
        }
    }
}
