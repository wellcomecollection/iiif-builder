using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class WorkflowCallController : Controller
    {
        private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly ILogger<WorkflowCallController> logger;
        private readonly DdsOptions ddsOptions;
        private readonly IAmazonSQS sqsClient;
        private readonly IIdentityService identityService;

        public WorkflowCallController(
            IWorkflowCallRepository workflowCallRepository,
            ILogger<WorkflowCallController> logger,
            IOptions<DdsOptions> options,
            IAmazonSQS sqsClient,
            IIdentityService identityService)
        {
            this.workflowCallRepository = workflowCallRepository;
            this.logger = logger;
            this.ddsOptions = options.Value;
            this.sqsClient = sqsClient;
            this.identityService = identityService;
        }

        public async Task<ActionResult> Recent()
        {
            ViewBag.IsErrorList = false;
            var recent = await workflowCallRepository.GetRecent(1000);
            return View("WorkflowCallList", recent);
        }
        
        public async Task<ActionResult> Errors()
        {
            ViewBag.IsErrorList = true;
            var errors = await workflowCallRepository.GetRecentErrors(500);
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


        public async Task<ActionResult> PutWorkflowMessageOnQueue(string id)
        {
            var ddsId = identityService.GetIdentity(id);
            string queueName = ddsId.StorageSpace switch
            {
                StorageSpace.Digitised => ddsOptions.DashboardPushDigitisedQueue,
                StorageSpace.BornDigital => ddsOptions.DashboardPushBornDigitalQueue,
                _ => null
            };

            if (queueName.IsNullOrWhiteSpace())
            {
                var errorMessage = $"No queue specified for workflow; could not notify for '{ddsId}'";
                logger.LogError(errorMessage);
                TempData["new-workflow-notification-error"] = errorMessage;
                return RedirectToAction("WorkflowCall", new {id});
            }

            try
            {
                var message = new WorkflowMessage
                {
                    Identifier = ddsId.PackageIdentifier,
                    Origin = "dashboard",
                    Space = ddsId.StorageSpace,
                    TimeSent = DateTime.UtcNow
                };
            
                logger.LogDebug("Simulating workflow SQS call from dashboard for {Identifier}", ddsId);
                var serialiserSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                
                
                var queueUrlResult = await sqsClient.GetQueueUrlAsync(queueName);
                var queueUrl = queueUrlResult.QueueUrl;
                
                var request = new SendMessageRequest(queueUrl, JsonConvert.SerializeObject(message, serialiserSettings));
                var response = await sqsClient.SendMessageAsync(request);
                logger.LogDebug(
                    "Received statusCode {StatusCode} for sending SQS for {Identifier} - {MessageId}",
                    response.HttpStatusCode, ddsId, response.MessageId);
                
                TempData["new-workflow-notification"] = $"Workflow notification sent for '{ddsId}'";
                return RedirectToAction("WorkflowCall", new {id});

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error making workflow queue call for '{id}'", id);
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


        public async Task<IActionResult> Bulk(IFormFile identifiersFile)
        {
            var model = new BulkWorkflowModel();
            if (identifiersFile is { Length: > 0 })
            {
                using (var reader = new StreamReader(identifiersFile.OpenReadStream()))
                {
                    model.Identifiers = await reader.ReadToEndAsync();
                }

                model.TidyIdentifiers();
            }

            return View(model);
        }

        public IActionResult BulkAnalyse(string identifiers)
        {
            var model = new BulkWorkflowModel
            {
                Identifiers = identifiers
            };
            model.TidyIdentifiers(true);
            if (model.DdsIdentifiers.HasItems())
            {
                return View(model);
            }

            return View("Bulk", model);

        }

        public async Task<IActionResult> BulkWorkflow(BulkWorkflowModel model)
        {
            if (model.RunnerOptions.HasWorkToDo())
            {
                var options = model.RunnerOptions.ToInt32();
                model.TidyIdentifiers(true);
                model.WorkflowJobs = new List<WorkflowJob>();
                try
                {
                    foreach (var ddsIdentifier in model.DdsIdentifiers)
                    {
                        var workflowJob = await workflowCallRepository.CreateWorkflowJob(
                            ddsIdentifier.PackageIdentifier, options);
                        model.WorkflowJobs.Add(workflowJob);
                    }
                }
                catch (Exception e)
                {
                    model.Error = e.Message;
                    logger.LogError(e, "Could not create a workflow job, " +
                                       "managed {WorkflowJobsCount} out of {DdsIdentifiersCount}", 
                        model.WorkflowJobs.Count, model.DdsIdentifiers.Count);
                }
            }
            else
            {
                model.Error = "No actions selected for jobs";
            }

            return View(model);
        }
        
        
        public async Task<IActionResult> Delete(string id)
        { 
            await workflowCallRepository.DeleteJob(id);
            TempData["job-deleted"] = $"{id} deleted.";
            return RedirectToAction("WorkflowCall", new {id});
        }
    }
}