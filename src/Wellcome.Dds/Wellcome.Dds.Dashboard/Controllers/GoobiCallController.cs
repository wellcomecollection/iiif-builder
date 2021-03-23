using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class GoobiCallController : Controller
    {
        private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly ILogger<GoobiCallController> logger;

        public GoobiCallController(
            IWorkflowCallRepository workflowCallRepository,
            ILogger<GoobiCallController> logger)
        {
            this.workflowCallRepository = workflowCallRepository;
            this.logger = logger;
        }

        public async Task<ActionResult> Recent()
        {
            ViewBag.IsErrorList = false;
            var recent = await workflowCallRepository.GetRecent(100);
            return View("GoobiCallList", recent);
        }
        
        public async Task<ActionResult> Errors()
        {
            ViewBag.IsErrorList = true;
            var errors = await workflowCallRepository.GetRecentErrors(100);
            return View("GoobiCallList", errors);
        }

        public async Task<ActionResult> Stats()
        {
            var stats = await workflowCallRepository.GetStatsModel();
            return View(stats);
        }

        public async Task<ActionResult> GoobiCall(string id)
        {
            var job = await workflowCallRepository.GetWorkflowJob(id);
            if (job == null)
            {
                job = new WorkflowJob
                {
                    Identifier = id,
                    Created = null
                };
            }

            return View(job);
        }

        public async Task<ActionResult> Create(string id)
        {
            var opts = Request.Query["options"].ToString();
            int? workflowOptions = null;
            if (opts.HasText())
            {
                if (int.TryParse(opts, out var options))
                {
                    workflowOptions = options;
                }
            }
            try
            {
                var workflowJob = await workflowCallRepository.CreateWorkflowJob(id, workflowOptions);
                TempData["new-workflow-job"] = $"Job Created: {workflowJob.Created}";
                return RedirectToAction("GoobiCall", new {id});
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error simulating Goobi call for '{id}'", id);
                TempData["new-workflow-job-error"] = e.Message;
                return RedirectToAction("GoobiCall", new {id});
            }
        }
    }
}