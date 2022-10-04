using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class WorkflowCallController : Controller
    {
        private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly ILogger<WorkflowCallController> logger;

        public WorkflowCallController(
            IWorkflowCallRepository workflowCallRepository,
            ILogger<WorkflowCallController> logger)
        {
            this.workflowCallRepository = workflowCallRepository;
            this.logger = logger;
        }

        public async Task<ActionResult> Recent()
        {
            ViewBag.IsErrorList = false;
            var recent = await workflowCallRepository.GetRecent(100);
            return View("WorkflowCallList", recent);
        }
        
        public async Task<ActionResult> Errors()
        {
            ViewBag.IsErrorList = true;
            var errors = await workflowCallRepository.GetRecentErrors(100);
            return View("WorkflowCallList", errors);
        }
        
        public async Task<ActionResult> MatchingErrors(string msg)
        {
            ViewBag.ErrorString = msg;
            var errorCount = await workflowCallRepository.CountMatchingErrors(msg);
            return View("MatchingErrors", errorCount);
        }

        public async Task<ActionResult> Stats()
        {
            var stats = await workflowCallRepository.GetStatsModel();
            return View(stats);
        }

        public async Task<ActionResult> WorkflowCall(string id)
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
                return RedirectToAction("WorkflowCall", new {id});
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error simulating workflow call for '{id}'", id);
                TempData["new-workflow-job-error"] = e.Message;
                return RedirectToAction("WorkflowCall", new {id});
            }
        }

        public async Task<IActionResult> ResetMatchingErrors(string resetWithMessage)
        {
            if (resetWithMessage.HasText())
            {
                int jobsReset = await workflowCallRepository.ResetJobsMatchingError(resetWithMessage);
                TempData["reset-errors"] = $"{jobsReset} Jobs with errors reset.";
            }
            return RedirectToAction("Errors");
        }
    }
}