using System;

namespace Wellcome.Dds.AssetDomain
{
    public interface IArchiveStorageStoredFileInfo
    {
        // fill this out with what callers need
        // could be backed by S3, file system.
        // comes from factory
        DateTime LastWriteTime { get; }
        string Uri { get; }
        string RelativePath { get; }
    }
}
