using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wellcome.Dds.AssetDomainRepositories;

namespace Wellcome.Dds.Server.Controllers
{
    public class WorkflowController : ControllerBase
    {
        private readonly DdsInstrumentationContext instrumentationContext;
        private readonly ILogger<WorkflowController> logger;

        public WorkflowController(DdsInstrumentationContext instrumentationContext, ILogger<WorkflowController> logger)
        {
            this.instrumentationContext = instrumentationContext;
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
        /// <param name="forceRebuild">if true, forces text resources to be rebuilt.</param>
        /// <returns>201 if accepted, else error.</returns>
        [HttpGet]
        [ProducesResponseType(202)]
        public async Task<ActionResult> Process(string id, bool forceRebuild = true)
        {
            try
            {
                var workflowJob = await instrumentationContext.PutJob(id, forceRebuild, false, -1, false, false);
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