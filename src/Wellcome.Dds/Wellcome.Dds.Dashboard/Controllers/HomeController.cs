using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDashboardRepository dashboardRepository;

        public HomeController(IDashboardRepository dashboardRepository)
        {
            this.dashboardRepository = dashboardRepository;
        }

        [Authorize]
        public async Task<IActionResult> IndexAsync(string id)
        {
            if(!id.HasText())
            {
                return RedirectToAction("Index", new { id = "b18402768" });
            }
            IDigitisedResource dgResource = await dashboardRepository.GetDigitisedResourceAsync(id);

            if (!(dgResource is IDigitisedManifestation))
            {
                var model = new TestModel { Message = "Only manifestations so far, no collections" };
                return View(model);
            }
            else
            {
                var dgManifestation = dgResource as IDigitisedManifestation;
                var syncOperation = dashboardRepository.GetDlcsSyncOperation(dgManifestation, true);
                // We want to have these running AT THE SAME TIME...
                // Not sure that's possible as we need the image list.
                // still, there are other things that can be async.
                var model = new TestModel { 
                    Manifestation = dgResource as IDigitisedManifestation,
                    SyncOperation = syncOperation,
                    DefaultSpace = dashboardRepository.DefaultSpace
                };
                return View(model);
            }
        }

        public IActionResult Open() => Ok("Everyone can see this");
    }
}
