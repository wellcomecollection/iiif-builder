using System;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace WorkflowProcessor
{
    public interface IWorkflowJobPostProcessor
    {
        Task PostProcess(WorkflowJob job);
    }

    public class WorkflowJobPostProcessor : IWorkflowJobPostProcessor
    {
        private readonly IAmazonSimpleNotificationService simpleNotificationService;
        private readonly UriPatterns uriPatterns;
        private readonly IOptions<CacheInvalidationOptions> options;
        private readonly ILogger<WorkflowJobPostProcessor> logger;

        public WorkflowJobPostProcessor(
            IAmazonSimpleNotificationService simpleNotificationService,
            UriPatterns uriPatterns,
            IOptions<CacheInvalidationOptions> options,
            ILogger<WorkflowJobPostProcessor> logger)
        {
            this.simpleNotificationService = simpleNotificationService;
            this.uriPatterns = uriPatterns;
            this.options = options;
            this.logger = logger;
        }

        public async Task PostProcess(WorkflowJob job)
        {
            if (!job.FlushCache) return;
            
            logger.LogInformation("Flushing cache for {Identifier}", job.Identifier);

            var cacheInvalidationOptions = options.Value;

            var runnerOptions = RunnerOptions.FromInt32(job.WorkflowOptions ?? 0);

            // api.wc.org cache is only invalidated if text has been rebuilt 
            if (runnerOptions.RebuildTextCaches)
            {
                var apiPaths =
                    uriPatterns.GetCacheInvalidationPaths(job.Identifier, InvalidationPathType.Text);
                await PublishInvalidationTopic(job.Identifier, cacheInvalidationOptions.InvalidateApiTopicArn,
                    apiPaths);
            }

            // iiif.wc.org cache is always invalidated
            var iiifPaths = uriPatterns.GetCacheInvalidationPaths(job.Identifier, InvalidationPathType.IIIF);
            await PublishInvalidationTopic(job.Identifier, cacheInvalidationOptions.InvalidateIIIFTopicArn, iiifPaths);
        }

        private async Task PublishInvalidationTopic(string identifier, string topic, string[] paths)
        {
            try
            {
                var messageObject = new
                {
                    paths = paths,
                    reference = $"dds-{identifier}-{DateTime.UtcNow.Ticks}"
                };
                var request = new PublishRequest(topic, JsonConvert.SerializeObject(messageObject));
                var response = await simpleNotificationService.PublishAsync(request);

                logger.LogDebug(
                    "Received statusCode {StatusCode} for publishing invalidation for {Identifier} - {MessageId}",
                    response.HttpStatusCode, identifier, response.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending invalidation request for {Identifier} to {Topic}",
                    identifier, topic);
            }
        }
    }
}