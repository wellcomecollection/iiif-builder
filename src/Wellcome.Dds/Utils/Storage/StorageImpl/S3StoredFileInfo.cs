using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Storage.StorageImpl
{
    /// <summary>
    // This returns an object that doesn't talk to S3 unless it needs to.
    // That is, calls to LastWriteTime or Exists should be lazy.
    /// </summary>
    public class S3StoredFileInfo : ISimpleStoredFileInfo
    {
        public DateTime LastWriteTime => throw new NotImplementedException();

        public string Uri => throw new NotImplementedException();

        bool ISimpleStoredFileInfo.Exists => throw new NotImplementedException();
    }
}
