using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Utils;
using Utils.Web;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.Common;

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
        /// <returns>202 if accepted, else error.</returns>
        [HttpGet]
        [ProducesResponseType(202)]
        public async Task<ActionResult> Process(string id, bool forceRebuild = true)
        {
            logger.LogInformation($"Received workflow/process instruction for {id}");
            if (!id.IsBNumber())
            {
                var message = $"{id} is not a b number.";
                logger.LogError(message);
                return StatusCode(500, message);
            }

            if (id.Equals(KnownIdentifiers.ChemistAndDruggist, StringComparison.InvariantCultureIgnoreCase))
            {
                const string message = "You can't rebuild Chemist and Druggist this way.";
                logger.LogWarning(message);
                return StatusCode(403, message);
            }
            try
            {
                var workflowJob = await instrumentationContext.PutJob(id, forceRebuild, false, -1, false, false);
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