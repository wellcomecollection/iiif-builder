using System;
using System.Net;
using System.Threading;
using Amazon.S3;
using Amazon.S3.Model;
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

        private void EnsureObjectMetadata()
        {
            if (exists.HasValue)
            {
                return;
            }

            var resp = amazonS3.GetObjectMetadataAsync(bucket, key);
            // OK, not async here...
            var metadataResult = resp.Result;
            if (metadataResult.HttpStatusCode == HttpStatusCode.OK)
            {
                exists = true;
                lastWriteTime = metadataResult.LastModified;
            }
            else
            {
                exists = false;
            }
        }

        public DateTime LastWriteTime
        {
            get
            {
                EnsureObjectMetadata();
                return lastWriteTime ?? DateTime.MinValue;
            }
        }

        public bool Exists
        {
            get
            {
                EnsureObjectMetadata();
                return exists != null && exists.Value;
            }
        }
        
        public string Uri { get; }

        public string Container => bucket;
        public string Path => key;
    }
}
