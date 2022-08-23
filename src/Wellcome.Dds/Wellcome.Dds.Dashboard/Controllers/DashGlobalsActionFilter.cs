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
        private readonly IDigitalObjectRepository digitalObjectRepository;

        public DashGlobalsActionFilter(IStatusProvider statusProvider, IDigitalObjectRepository digitalObjectRepository)
        {
            this.statusProvider = statusProvider;
            this.digitalObjectRepository = digitalObjectRepository;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.Controller as Controller;
            var viewBag = controller.ViewBag;
            var runProcesses = await statusProvider.ShouldRunProcesses();
            viewBag.StopClass = runProcesses ? "" : "btn-danger";
            var heartbeat = await statusProvider.GetHeartbeat();
            viewBag.Heartbeat = heartbeat;
            var warningState = heartbeat == null || heartbeat.Value.AddMinutes(3) < DateTime.Now;
            viewBag.HeartbeatWarning = warningState;
            viewBag.HeartbeatClass = warningState ? "btn-danger" : "";
            var getQueueLevel = digitalObjectRepository.GetDlcsQueueLevel();
            await next();

            try
            {
                viewBag.QueueLevels = await getQueueLevel;
            }
            catch (Exception)
            {
                // can happen due to timeouts
                viewBag.QueueLevels = new Dictionary<string, long>
                {
                    ["incoming"] = -1,
                    ["priority"] = -1
                };
            }
        }
    }
}