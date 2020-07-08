using System;
using Utils.Storage;

namespace Utils.Aws.S3
{
    /// <summary>
    // This returns an object that doesn't talk to S3 unless it needs to.
    // That is, calls to LastWriteTime or Exists should be lazy.

    // It behaves like a FileInfo object.
    /// </summary>
    public class S3StoredFileInfo : ISimpleStoredFileInfo
    {
        public DateTime LastWriteTime => throw new NotImplementedException();

        public string Uri => throw new NotImplementedException();

        bool ISimpleStoredFileInfo.Exists => throw new NotImplementedException();
    }
}
