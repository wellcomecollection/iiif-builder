using Amazon.S3;
using Microsoft.Extensions.Logging;
using System;

namespace Utils.Storage.S3
{
    public class S3CacheStorage : ICacheStorage
    {
        private ILogger<S3CacheStorage> logger;
        private string bucket;
        private IAmazonS3 amazonS3;

        public S3CacheStorage(
            ILogger<S3CacheStorage> logger,
            IAmazonS3ForCacheStorage amazonS3)
        {
            this.logger = logger;
            this.amazonS3 = amazonS3;
        }

        public string Folder
        {
            get { return bucket; }
            set { bucket = value; }
        }

        public void DeleteCacheFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public ISimpleStoredFileInfo GetCachedFile(string fileName)
        {
            // This returns an object that doesn't talk to S3 unless it needs to.
            // That is, calls to LastWriteTime or Exists should be lazy.
            throw new NotImplementedException();
        }

        public T Read<T>(ISimpleStoredFileInfo fileInfo) where T : class
        {
            throw new NotImplementedException();
        }

        public void Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class
        {
            throw new NotImplementedException();
        }
    }
}
