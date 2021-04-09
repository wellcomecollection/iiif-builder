using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class WorkflowJobController : Controller
    {
        private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly ILogger<WorkflowJobController> logger;

        public WorkflowJobController(IWorkflowCallRepository workflowCallRepository,
            ILogger<WorkflowJobController> logger)
        {
            this.workflowCallRepository = workflowCallRepository;
            this.logger = logger;
        }
        
        /// <summary>
        /// Create a new workflow job to rebuild _all_ elements of job
        /// </summary>
        /// <param name="id">work identifier</param>
        [HttpPost]
        public Task<ActionResult> RefreshAll(string id)
            => QueueWorkflowJob(id, true, "RefreshAllResult");

        /// <summary>
        /// Create a new workflow job to rebuild manifest and iiif elements of job
        /// </summary>
        /// <param name="id">work identifier</param>
        [HttpPost]
        public Task<ActionResult> RefreshIIIF(string id)
            => QueueWorkflowJob(id, false, "RefreshIIIFResult");
        
        private async Task<ActionResult> QueueWorkflowJob(string id, bool iiifOnly, string tempDataType)
        {
            bool success = true;
            string message = null;
            
            try
            {
                var bNumber = new DdsIdentifier(id).BNumber;
                await workflowCallRepository.CreateExpeditedWorkflowJob(bNumber, iiifOnly ? 6 : null, true);
            }
            catch (Exception ex)
            {
                success = false;
                message = ex.Message;
                logger.LogError(ex, "QueueWorkflowJob error for {Id}", id);
            }

            var deleteResult = new DeleteResult
            {
                Success = success,
                Message = message,
            };
            TempData[tempDataType] = JsonConvert.SerializeObject(deleteResult);

            return RedirectToAction("Manifestation", "Dash", new {id});
        }
    }
}