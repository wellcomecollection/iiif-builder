using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils.Aws.Options;
using Utils.Storage;

namespace Utils.Aws.S3
{
    /// <summary>
    /// Implementation of <see cref="IStorage"/> that uses S3 for backing store but is aware of both binary and protobuf
    /// serialisation targets
    /// </summary>
    /// <remarks>This is temporary until all services are switched to using protobuf</remarks>
    public class S3CacheAwareStorage : IStorage
    {
        private readonly ILogger<S3CacheAwareStorage> logger;
        private readonly IAmazonS3 amazonS3;
        private readonly S3CacheOptions options;

        public S3CacheAwareStorage(
            ILogger<S3CacheAwareStorage> logger,
            IAmazonS3 amazonS3,
            IOptions<S3CacheOptions> options
        )
        {
            this.logger = logger;
            this.amazonS3 = amazonS3;
            this.options = options.Value;
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
            var path = options.ReadProtobuf ? GetProtobufKey(fileInfo.Path) : fileInfo.Path;
            try
            {
                var sw = Stopwatch.StartNew();
                await using var stream = await GetStream(fileInfo.Container, path);
                logger.LogDebug("Read stream from '{Bucket}/{Path}' in {Elapsed}ms", fileInfo.Container, path,
                    sw.ElapsedMilliseconds);
                if (stream != null)
                {
                    sw.Reset();
                    var obj = Deserialize<T>(stream, fileInfo);
                    logger.LogDebug("Deserialized stream from '{Bucket}/{Path}' in {Elapsed}ms", fileInfo.Container,
                        path,
                        sw.ElapsedMilliseconds);
                    sw.Stop();
                    stream.Close();
                    return obj;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Attempt to deserialize '{Bucket}/{Path}' from S3 failed", fileInfo.Container, path);
            }

            return default;
        }

        public async Task Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class
        {
            try
            {
                var sw = Stopwatch.StartNew();
                if (options.WriteBinary)
                {
                    throw new NotSupportedException("Binary Serializer is no longer supported");
                }

                if (options.WriteProtobuf)
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = fileInfo.Container, Key = GetProtobufKey(fileInfo.Path)
                    };
                    logger.LogDebug("Writing protobuf cache file '{Bucket}/{Path}' to S3", request.BucketName,
                        request.Key);

                    await using (request.InputStream = new MemoryStream())
                    {
                        sw.Restart();
                        
                        ProtoBuf.Serializer.Serialize(request.InputStream, t);
                        await amazonS3.PutObjectAsync(request);
                        
                        logger.LogDebug("Wrote stream for '{Bucket}/{Path}' in {Elapsed}ms", request.BucketName,
                            request.Key,
                            sw.ElapsedMilliseconds);
                    }
                }
                
                sw.Stop();
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

        private T Deserialize<T>(Stream source, ISimpleStoredFileInfo fileInfo)
        {
            // TODO - temporary until all caches are read/written as protobuf
            if (options.ReadProtobuf)
            {
                return ProtoBuf.Serializer.Deserialize<T>(source);
            }

            throw new NotSupportedException("Binary Serializer is no longer supported");
        }

        // TODO - having this knowledge here isn't great but is only temporary until everything is moved to protobuf
        private string GetProtobufKey(string key) => key.Replace(".ser", ".bin");
    }
}