using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.AssetDomainRepositories.Workflow
{
    public class CacheInvalidationPathPublisher : ICacheInvalidationPathPublisher
    {
        private readonly IAmazonSimpleNotificationService simpleNotificationService;
        private readonly UriPatterns uriPatterns;
        private readonly IOptions<CacheInvalidationOptions> options;
        private readonly ILogger<CacheInvalidationPathPublisher> logger;

        public CacheInvalidationPathPublisher(
            IAmazonSimpleNotificationService simpleNotificationService,
            UriPatterns uriPatterns,
            IOptions<CacheInvalidationOptions> options,
            ILogger<CacheInvalidationPathPublisher> logger)
        {
            this.simpleNotificationService = simpleNotificationService;
            this.uriPatterns = uriPatterns;
            this.options = options;
            this.logger = logger;
        }

        public async Task<string[]> PublishInvalidation(string identifier, bool includeTextResources)
        {
            logger.LogInformation("Flushing cache for {Identifier}", identifier);

            var cacheInvalidationOptions = options.Value;
            var messages = new List<string>();

            // api.wc.org cache is only invalidated if text has been rebuilt 
            if (includeTextResources && new DdsIdentifier(identifier).HasBNumber)
            {
                if (cacheInvalidationOptions.InvalidateApiTopicArn.IsNullOrWhiteSpace())
                {
                    messages.Add("Ignoring api.wellcomecollection.org paths, no topic ARN configured");
                }
                else
                {
                    // Only bnumbers will have text that needs flushing, for now, so don't bother calling if not a bnumber.
                    var apiPaths =
                        uriPatterns.GetCacheInvalidationPaths(identifier, InvalidationPathType.Text);
                    var apiMessage = await PublishInvalidationTopic(
                        identifier, cacheInvalidationOptions.InvalidateApiTopicArn, apiPaths);
                    if (apiMessage.HasText())
                    {
                        messages.Add(apiMessage);
                    }
                }
            }

            if (cacheInvalidationOptions.InvalidateIIIFTopicArn.IsNullOrWhiteSpace())
            {
                messages.Add("Ignoring iiif.wellcomecollection.org paths, no topic ARN configured");
            }
            else
            {
                // iiif.wc.org cache is always invalidated
                var iiifPaths = uriPatterns.GetCacheInvalidationPaths(identifier, InvalidationPathType.IIIF);
                var iiifMessage = await PublishInvalidationTopic(
                    identifier, cacheInvalidationOptions.InvalidateIIIFTopicArn, iiifPaths);
                if (iiifMessage.HasText())
                {
                    messages.Add(iiifMessage);
                }
            }

            return messages.ToArray();
        }

        private async Task<string?> PublishInvalidationTopic(string identifier, string topic, string[] paths)
        {
            string? message = null;
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
                message = ex.Message;
            }

            return message;
        }
    }
}