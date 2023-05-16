using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Utils.Storage;

namespace Utils.Aws.S3
{
    public class S3Storage : IStorage
    {
        private readonly ILogger<S3Storage> logger;
        private readonly IAmazonS3 amazonS3;

        public S3Storage(
            ILogger<S3Storage> logger,
            IAmazonS3 amazonS3
        )
        {
            this.logger = logger;
            this.amazonS3 = amazonS3;
        }

        public ISimpleStoredFileInfo GetCachedFileInfo(string container, string fileName)
        {
            // This returns an object that doesn't talk to S3 unless it needs to.
            // That is, calls to LastWriteTime or Exists should be lazy.
            return new S3StoredFileInfo(container, fileName, amazonS3);
        }

        public Task DeleteCacheFile(string container, string fileName) 
            => amazonS3.DeleteObjectAsync(container, fileName);

        public async Task<T?> Read<T>(ISimpleStoredFileInfo fileInfo) where T : class
        {
            if (fileInfo.Container.IsNullOrWhiteSpace())
            {
                logger.LogError("No Container specified for ISimpleStoredFileInfo object");
                return default;
            }
            try
            {
                await using var stream = await GetStream(fileInfo.Container, fileInfo.Path);
                if (stream != null)
                {
                    var obj = ProtoBuf.Serializer.Deserialize<T>(stream);
                    stream.Close();
                    return obj;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Attempt to deserialize '{Uri}' from S3 failed", fileInfo.Uri);
            }
            return default;
        }

        public async Task Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class
        {
            logger.LogInformation("Writing cache file '{Uri}' to S3", fileInfo.Uri);
            var request = new PutObjectRequest
            {
                BucketName = fileInfo.Container, Key = fileInfo.Path
            };
            
            try
            {
                await using (request.InputStream = new MemoryStream())
                {
                    ProtoBuf.Serializer.Serialize(request.InputStream, t);
                    await amazonS3.PutObjectAsync(request);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to write to file '{Uri}' to S3", fileInfo.Uri);
                if (writeFailThrowsException)
                {
                    throw;
                }
            }
        }

        public async Task<Stream?> GetStream(string container, string fileName)
        {
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = container,
                Key = fileName
            };
            try
            {
                var getResponse = await amazonS3.GetObjectAsync(getObjectRequest);
                return getResponse.ResponseStream;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug("Could not find S3 object {Bucket}/{Key}", container, fileName);
                return null;
            }
            catch (AmazonS3Exception e)
            {
                logger.LogWarning(e, "Could not copy S3 Stream for {S3ObjectRequest}; {StatusCode}",
                    getObjectRequest, e.StatusCode);
                throw;
            }
        }
    }
}
