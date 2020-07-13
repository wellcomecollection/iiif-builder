using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dashboard;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class DashGlobalsActionFilter : IAsyncActionFilter
    {
        private readonly IStatusProvider statusProvider;
        private readonly IDashboardRepository dashboardRepository;

        public DashGlobalsActionFilter(IStatusProvider statusProvider, IDashboardRepository dashboardRepository)
        {
            this.statusProvider = statusProvider;
            this.dashboardRepository = dashboardRepository;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.Controller as Controller;
            var viewBag = controller.ViewBag;
            viewBag.StopClass = statusProvider.RunProcesses ? "" : "btn-danger";
            var heartbeat = statusProvider.GetHeartbeat();
            viewBag.Heartbeat = heartbeat;
            var warningState = heartbeat == null || heartbeat.Value.AddMinutes(3) < DateTime.Now;
            viewBag.HeartbeatWarning = warningState;
            viewBag.HeartbeatClass = warningState ? "btn-danger" : "";
            var getQueueLevel = dashboardRepository.GetDlcsQueueLevel();
            await next();
            viewBag.QueueLevels = await getQueueLevel;
        }
    }
}