using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Utils;
using Utils.Web;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers
{
    public class WorkflowController : ControllerBase
    {
        private readonly ILogger<WorkflowController> logger;
        private readonly IWorkflowCallRepository workflowCallRepository;

        public WorkflowController(
            IWorkflowCallRepository workflowCallRepository,
            ILogger<WorkflowController> logger)
        {
            this.workflowCallRepository = workflowCallRepository;
            this.logger = logger;
        }

        /// <summary>
        /// Create a Workflow job record for specified bNumber
        /// </summary>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /workflow/process/b1675665
        /// 
        /// </remarks>
        /// <param name="id">bNumber to create job for.</param>
        /// <returns>202 if accepted, else error.</returns>
        [HttpGet]
        [ProducesResponseType(202)]
        public async Task<ActionResult> Process(string id)
        {
            logger.LogInformation($"Received workflow/process instruction for {id}");

            if (id.Equals(KnownIdentifiers.ChemistAndDruggist, StringComparison.InvariantCultureIgnoreCase))
            {
                const string message = "You can't rebuild Chemist and Druggist this way.";
                logger.LogWarning(message);
                return StatusCode(403, message);
            }
            try
            {
                var ddsId = new DdsIdentifier(id);
                var message = new WorkflowMessage
                {
                    Identifier = ddsId.PackageIdentifier,
                    Origin = "workflow-controller",
                    Space = ddsId.StorageSpace,
                    TimeSent = DateTime.Now
                };
                var workflowJob = await workflowCallRepository.CreateWorkflowJob(message);
                Response.AppendStandardNoCacheHeaders();
                logger.LogInformation($"Accepted workflow/process instruction for {id}");
                return StatusCode(202, workflowJob);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating WorkflowJob for bnumber '{bNumber}'", id);
                var errorResponse = new WorkFlowCallError
                {
                    Message = ex.Message,
                    StackTrace = ex.StackTrace
                };
                logger.LogError(errorResponse.Message, ex);
                return StatusCode(500, errorResponse);
            }
        }
        
        class WorkFlowCallError
        {
            public string Type => "Error";
            public string Message { get; set; }
            public string StackTrace { get; set; }
        }
    }
}