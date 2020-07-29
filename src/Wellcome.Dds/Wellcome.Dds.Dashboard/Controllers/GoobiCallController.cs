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
        
        public ActionResult GoobiCall(string id = null)
        {
            if (id == null)
            {
                ViewBag.IsErrorList = false;
                var top100 = workflowCallRepository.GetRecent();
                return View("GoobiCallList", top100);
            }
            if (id == "errors")
            {
                ViewBag.IsErrorList = true;
                var top100 = workflowCallRepository.GetRecentErrors();
                return View("GoobiCallList", top100);
            }
            if (id == "stats")
            {
                var stats = workflowCallRepository.GetStatsModel();
                return View("Stats", stats);
            }
            var job = workflowCallRepository.GetWorkflowJob(id);
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