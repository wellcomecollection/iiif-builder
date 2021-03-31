using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using IIIF;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.Common;

namespace WorkflowProcessor
{
    /// <summary>
    /// Utility class for Workflow writing objects to S3
    /// </summary>
    public class BucketWriter
    {
        private readonly DdsOptions ddsOptions;
        private readonly IAmazonS3 amazonS3;
        private readonly ILogger<BucketWriter> logger;

        public BucketWriter(ILogger<BucketWriter> logger, IAmazonS3 amazonS3,
            IOptions<DdsOptions> ddsOptions)
        {
            this.logger = logger;
            this.amazonS3 = amazonS3;
            this.ddsOptions = ddsOptions.Value;
        }

        public Task PutIIIFJsonObjectToS3(JsonLdBase iiifResource, string bucket, string key, string logLabel) 
            => WriteObjectToS3(
                bucket,
                key,
                iiifResource.AsJson(),
                "application/json",
                logLabel);

        public Task SaveRawTextToS3(string content, string key)
        {
            if (content.IsNullOrWhiteSpace())
            {
                return Task.CompletedTask;
            }

            return WriteObjectToS3(
                ddsOptions.TextContainer,
                key,
                content,
                "text/plain",
                "raw text");
        }

        private async Task WriteObjectToS3(string bucket, string key, string content, string contentType,
            string logLabel)
        {
            var put = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = content,
                ContentType = contentType
            };
            logger.LogInformation("Putting {LogLabel} to S3: bucket: {BucketName}, key: {Key}", logLabel,
                put.BucketName, put.Key);
            await amazonS3.PutObjectAsync(put);
        }
    }
}