using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDashboardRepository dashboardRepository;

        public HomeController(IDashboardRepository dashboardRepository)
        {
            this.dashboardRepository = dashboardRepository;
        }

        public async Task<IActionResult> Index()
        {
            var queueLevel = dashboardRepository.GetDlcsQueueLevel();
            return View((object)$"{queueLevel.Keys.First()} : {queueLevel.Values.First()}");
        }
    }
}
