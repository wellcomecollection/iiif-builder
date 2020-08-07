using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Wellcome.Dds.AssetDomain.Workflow;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class GoobiCallController : Controller
    {
        private readonly IWorkflowCallRepository workflowCallRepository;

        public GoobiCallController(
            IWorkflowCallRepository workflowCallRepository)
        {
            this.workflowCallRepository = workflowCallRepository;
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
            try
            {
                var workflowJob = await workflowCallRepository.CreateWorkflowJob(id);
                TempData["new-workflow-job"] = $"Job Created: {workflowJob.Created}";
                return RedirectToAction("GoobiCall", new {id});
            }
            catch (Exception e)
            {
                TempData["new-workflow-job-error"] = e.Message;
                return RedirectToAction("GoobiCall", new {id});
            }
        }
    }
}