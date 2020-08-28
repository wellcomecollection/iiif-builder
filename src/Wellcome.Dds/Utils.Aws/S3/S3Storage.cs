using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
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

        public async Task<T> Read<T>(ISimpleStoredFileInfo fileInfo) where T : class
        {
            T t = default;
            try
            {
                await using var stream = await GetStream(fileInfo.Container, fileInfo.Path);
                if (stream != null)
                {
                    IFormatter formatter = new BinaryFormatter();
                    var obj = formatter.Deserialize(stream);
                    stream.Close();
                    switch (obj)
                    {
                        case T tObj:
                            // The object from S3 is a T as expected, so we're good.
                            t = tObj;
                            break;
                        case null:
                            // not sure if this actually ever happens...
                            logger.LogError(
                                $"Attempt to deserialize '{fileInfo.Uri}' from S3 failed, stream didn't result in object");
                            break;
                        default:
                            // Something else wrote to the bucket? Not a T
                            logger.LogError(
                                $"Attempt to deserialize '{fileInfo.Uri}' from S3 failed, expected {typeof(T)}, found {obj.GetType()}");
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Attempt to deserialize '{Uri}' from S3 failed.", fileInfo.Uri);
            }
            return t;
        }

        public async Task Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class
        {
            logger.LogInformation("Writing cache file '{Uri}' to S3", fileInfo.Uri);
            var request = new PutObjectRequest()
            {
                BucketName = fileInfo.Container, Key = fileInfo.Path
            };
            
            try
            {
                IFormatter formatter = new BinaryFormatter();
                await using (request.InputStream = new MemoryStream())
                {
                    formatter.Serialize(request.InputStream, t);
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
                logger.LogInformation(e, "Could not find S3 object {Bucket}/{Key}", container, fileName);
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
