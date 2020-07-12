using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using Wellcome.Dds.AssetDomain.Dashboard;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class DashGlobalsActionFilter : IActionFilter
    {
        private readonly IStatusProvider statusProvider;
        private readonly IDashboardRepository dashboardRepository;

        public DashGlobalsActionFilter(IStatusProvider statusProvider, IDashboardRepository dashboardRepository)
        {
            this.statusProvider = statusProvider;
            this.dashboardRepository = dashboardRepository;
        }

        public void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controller = filterContext.Controller as Controller;
            var viewBag = controller.ViewBag;
            viewBag.StopClass = statusProvider.RunProcesses ? "" : "btn-danger";
            var heartbeat = statusProvider.GetHeartbeat();
            viewBag.Heartbeat = heartbeat;
            var warningState = heartbeat == null || heartbeat.Value.AddMinutes(3) < DateTime.Now;
            viewBag.HeartbeatWarning = warningState;
            viewBag.HeartbeatClass = warningState ? "btn-danger" : "";
            viewBag.QueueLevels = dashboardRepository.GetDlcsQueueLevel();
        }

        public void OnActionExecuted(ActionExecutedContext filterContext)
        {

        }
    }
}