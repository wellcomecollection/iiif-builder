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
        [Obsolete]
        [ProducesResponseType(202)]
        public async Task<ActionResult> Process(string id)
        {
            logger.LogInformation($"Received workflow/process instruction for {id}");
            logger.LogWarning("DDS will no longer accept workflow jobs on this endpoint. Use SQS instead.");
            
            // We will retain the format of the response until Goobi retires this call.
            var fakeJob = new WorkflowJob
            {
                Created = DateTime.UtcNow,
                Identifier = id
            };
            return StatusCode(202, fakeJob);
        }
    }
}