using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Storage.StorageImpl
{
    public class S3Storage : IStorage
    {
        private string bucket;

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
