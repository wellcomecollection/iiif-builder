using System;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace WorkflowProcessor
{
    public interface IIdentifierChangeNotificationPublisher
    {
        Task PostProcess(string identifier, bool includeTextResources);
    }

    public class IdentifierChangeNotificationPublisher : IIdentifierChangeNotificationPublisher
    {
        private readonly IAmazonSimpleNotificationService simpleNotificationService;
        private readonly UriPatterns uriPatterns;
        private readonly IOptions<CacheInvalidationOptions> options;
        private readonly ILogger<IdentifierChangeNotificationPublisher> logger;

        public IdentifierChangeNotificationPublisher(
            IAmazonSimpleNotificationService simpleNotificationService,
            UriPatterns uriPatterns,
            IOptions<CacheInvalidationOptions> options,
            ILogger<IdentifierChangeNotificationPublisher> logger)
        {
            this.simpleNotificationService = simpleNotificationService;
            this.uriPatterns = uriPatterns;
            this.options = options;
            this.logger = logger;
        }

        public async Task PostProcess(string identifier, bool includeTextResources)
        {
            logger.LogInformation("Flushing cache for {Identifier}", identifier);

            var cacheInvalidationOptions = options.Value;

            // api.wc.org cache is only invalidated if text has been rebuilt 
            if (includeTextResources && new DdsIdentifier(identifier).HasBNumber)
            {
                // Only bnumbers will have text that needs flushing, for now, so don't bother calling if not a bnumber.
                var apiPaths =
                    uriPatterns.GetCacheInvalidationPaths(identifier, InvalidationPathType.Text);
                await PublishInvalidationTopic(identifier, cacheInvalidationOptions.InvalidateApiTopicArn,
                    apiPaths);
            }

            // iiif.wc.org cache is always invalidated
            var iiifPaths = uriPatterns.GetCacheInvalidationPaths(identifier, InvalidationPathType.IIIF);
            await PublishInvalidationTopic(identifier, cacheInvalidationOptions.InvalidateIIIFTopicArn, iiifPaths);
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