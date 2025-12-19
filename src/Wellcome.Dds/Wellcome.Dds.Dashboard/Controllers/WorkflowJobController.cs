using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class WorkflowJobController : Controller
    {
        private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly ILogger<WorkflowJobController> logger;
        private readonly ICacheInvalidationPathPublisher invalidationPathPublisher;
        private readonly IIdentityService identityService;

        public WorkflowJobController(
            IWorkflowCallRepository workflowCallRepository,
            ILogger<WorkflowJobController> logger,
            ICacheInvalidationPathPublisher invalidationPathPublisher,
            IIdentityService identityService)
        {
            this.workflowCallRepository = workflowCallRepository;
            this.logger = logger;
            this.invalidationPathPublisher = invalidationPathPublisher;
            this.identityService = identityService;
        }
        
        /// <summary>
        /// Create a new workflow job to rebuild _all_ elements of job
        /// </summary>
        /// <param name="id">work identifier</param>
        [HttpPost]
        public Task<ActionResult> RefreshAll(string id)
            => QueueWorkflowJob(id, false, "RefreshAllResult");

        /// <summary>
        /// Create a new workflow job to rebuild manifest and iiif elements of job
        /// </summary>
        /// <param name="id">work identifier</param>
        [HttpPost]
        public Task<ActionResult> RefreshIIIF(string id)
            => QueueWorkflowJob(id, true, "RefreshIIIFResult");
        
        private async Task<ActionResult> QueueWorkflowJob(string id, bool iiifOnly, string tempDataType)
        {
            if (id.StartsWith(KnownIdentifiers.ChemistAndDruggist))
            {
                return RedirectToManifestation(id, tempDataType, false,
                    "Rebuilding Chemist and Druggist from UI not supported");
            }
            
            bool success = true;
            string message = null;
            
            try
            {
                var ddsId = identityService.GetIdentity(id);
                await workflowCallRepository.CreateExpeditedWorkflowJob(ddsId.PackageIdentifier, iiifOnly ? 6 : null, true);
            }
            catch (Exception ex)
            {
                success = false;
                message = ex.Message;
                logger.LogError(ex, "QueueWorkflowJob error for {Id}", id);
            }

            return RedirectToManifestation(id, tempDataType, success, message);
        }

        private ActionResult RedirectToManifestation(string id, string tempDataType, bool success, string message)
        {
            var ddsId = identityService.GetIdentity(id);
            var deleteResult = new DeleteResult
            {
                Success = success,
                Message = message,
            };
            TempData[tempDataType] = JsonConvert.SerializeObject(deleteResult);

            return RedirectToAction("Manifestation", "Dash", new {ddsId.PathElementSafe});
        }

        /// <summary>
        /// Notify the SNS topic that will call cloudfront API to invalidate caches
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hasText">Whether to clear text paths too</param>
        [HttpPost]
        public async Task<IActionResult> ClearCaches(string id, [FromForm] bool hasText = false)
        {
            var ddsId = identityService.GetIdentity(id);
            string[] errors;
            try
            {
                errors = await invalidationPathPublisher.PublishInvalidation(ddsId.Value, hasText);
            }
            catch (Exception ex)
            {
                errors = new[] { ex.Message };
            }

            if (errors.HasItems())
            {
                var message = string.Join(';', errors);
                return RedirectToManifestation(id, "ClearCachesResult", false, message);
            }

            return RedirectToManifestation(id, "ClearCachesResult", true, null);
        }
    }
}