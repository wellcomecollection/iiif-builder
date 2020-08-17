using Microsoft.AspNetCore.Mvc;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class AccountController : Controller
    {
        private readonly IDashboardRepository dashboardRepository;

        public AccountController(IDashboardRepository dashboardRepository)
        {
            this.dashboardRepository = dashboardRepository;
        }

        public ActionResult UserPage(string id = null)
        {
            var username = id.HasText() ? id : User.Identity.Name;
            var recentActions = dashboardRepository.GetRecentActions(200, username);
            ViewBag.Subject = username;
            return View(recentActions);
        }
    }
}
