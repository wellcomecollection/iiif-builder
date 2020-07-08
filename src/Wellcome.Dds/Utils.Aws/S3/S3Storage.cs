using System;
using System.IO;
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

        public string Container { get; set; }

        public ISimpleStoredFileInfo GetCachedFile(string fileName)
        {
            // This returns an object that doesn't talk to S3 unless it needs to.
            // That is, calls to LastWriteTime or Exists should be lazy.
            return new S3StoredFileInfo(Container, fileName, amazonS3);
        }
        
        public void DeleteCacheFile(string fileName)
        {
            amazonS3.DeleteObjectAsync(Container, fileName);
        }


        public async Task<T> Read<T>(ISimpleStoredFileInfo fileInfo) where T : class
        {
            T t = default(T);
            try
            {
                var getResponse = await amazonS3.GetObjectAsync(fileInfo.Container, fileInfo.Path);
                await using (var stream = getResponse.ResponseStream)
                {
                    IFormatter formatter = new BinaryFormatter();
                    stream.Position = 0;
                    t = formatter.Deserialize(stream) as T;
                    stream.Close();
                }
                if (t == null)
                {
                    logger.LogError($"Attempt to deserialize '{fileInfo.Uri}' from S3 failed.");
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Attempt to deserialize '{fileInfo.Uri}' from S3 failed.", e);
            }
            return t;
        }

        public async Task Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class
        {
            logger.LogInformation("Writing cache file '" + fileInfo.Uri + "' to S3");
            var request = new PutObjectRequest()
            {
                BucketName = fileInfo.Container, Key = fileInfo.Path
            };
            
            try
            {
                IFormatter formatter = new BinaryFormatter();
                await using (request.InputStream =  new MemoryStream())
                {
                    formatter.Serialize(request.InputStream, t);
                    var response = await amazonS3.PutObjectAsync(request);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Unable to write to file '{fileInfo.Uri}' to S3", ex);
                if (writeFailThrowsException)
                {
                    throw;
                }
            }
        }
    }
}
