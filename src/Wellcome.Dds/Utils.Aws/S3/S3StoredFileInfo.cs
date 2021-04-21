using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Utils.Storage;

namespace Utils.Aws.S3
{
    /// <summary>
    /// This returns an object that doesn't talk to S3 unless it needs to.
    /// That is, calls to LastWriteTime or Exists should be lazy.
    /// It behaves like a FileInfo object.
    /// </summary>
    public class S3StoredFileInfo : ISimpleStoredFileInfo
    {
        private readonly IAmazonS3 amazonS3;
        private readonly string bucket;
        private readonly string key;
        private DateTime? lastWriteTime;
        private bool? exists;

        public S3StoredFileInfo(string bucket, string key, IAmazonS3 amazonS3)
        {
            this.bucket = bucket;
            this.key = key;
            this.amazonS3 = amazonS3;
            Uri = $"s3://{bucket}/{key}";
        }

        public async Task EnsureObjectMetadata()
        {
            if (exists.HasValue)
            {
                return;
            }
            
            // The "right" way to do this would be with a S3FileInfo.Exists, but that is not available
            // in .NET Core. So we need to test by exception catching on GetObjectMetadataAsync.
            
            // However, that's what S3FileInfo.Exists does anyway, so we're not any better off.
            // https://github.com/aws/aws-sdk-net/blob/master/sdk/src/Services/S3/Custom/_bcl/IO/S3FileInfo.cs#L118

            try
            {
                var metadataResult = await amazonS3.GetObjectMetadataAsync(bucket, key);
                if (metadataResult.HttpStatusCode == HttpStatusCode.OK)
                {
                    exists = true;
                    lastWriteTime = metadataResult.LastModified;
                    return;
                }
            }
            catch
            {
                // ignored
            }
            exists = false;
        }

        public async Task<bool> DoesExist()
        {
            await EnsureObjectMetadata();
            return exists != null && exists.Value;
        }

        public async Task<DateTime?> GetLastWriteTime()
        {
            await EnsureObjectMetadata();
            return lastWriteTime;
        }

        public string Uri { get; }

        public string Container => bucket;
        public string Path => key;
    }
}
