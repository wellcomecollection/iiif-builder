using System;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class WorkflowCallController : Controller
    {
        private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly ILogger<WorkflowCallController> logger;
        private readonly StorageOptions storageOptions;
        private readonly IAmazonSimpleNotificationService simpleNotificationService;

        public WorkflowCallController(
            IWorkflowCallRepository workflowCallRepository,
            ILogger<WorkflowCallController> logger,
            IOptions<StorageOptions> options,
            IAmazonSimpleNotificationService simpleNotificationService)
        {
            this.workflowCallRepository = workflowCallRepository;
            this.logger = logger;
            this.storageOptions = options.Value;
            this.simpleNotificationService = simpleNotificationService;
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
            var ddsId = new DdsIdentifier(id);
            var job = await workflowCallRepository.GetWorkflowJob(ddsId);
            if (job == null)
            {
                job = new WorkflowJob
                {
                    Identifier = ddsId,
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


        public async Task<ActionResult> NotifyTopic(string id)
        {
            var ddsId = new DdsIdentifier(id);
            var topic = storageOptions.WorkflowMessageTopic;
            if (topic.IsNullOrWhiteSpace())
            {
                var errorMessage = $"No topic specified for workflow; could not notify for '{ddsId}'";
                logger.LogError(errorMessage);
                TempData["new-workflow-notification-error"] = errorMessage;
                return RedirectToAction("WorkflowCall", new {id});
            }

            try
            {
                var message = new WorkflowMessage
                {
                    Identifier = ddsId,
                    Origin = "dashboard",
                    Space = ddsId.StorageSpace,
                    TimeSent = DateTime.Now
                };
            
                logger.LogDebug("Simulating workflow SNS call from dashboard for {Identifier}", ddsId);
                var serialiserSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var request = new PublishRequest(topic, JsonConvert.SerializeObject(message, serialiserSettings));
                var response = await simpleNotificationService.PublishAsync(request);
                logger.LogDebug(
                    "Received statusCode {StatusCode} for publishing invalidation for {Identifier} - {MessageId}",
                    response.HttpStatusCode, ddsId, response.MessageId);
                
                TempData["new-workflow-notification"] = $"Workflow notification sent for '{ddsId}'";
                return RedirectToAction("WorkflowCall", new {id});

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error making workflow topic notification for '{id}'", id);
                TempData["new-workflow-notification-error"] = e.Message;
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