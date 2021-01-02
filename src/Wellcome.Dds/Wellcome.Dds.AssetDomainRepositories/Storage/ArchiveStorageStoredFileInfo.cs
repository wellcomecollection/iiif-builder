using System;
using Wellcome.Dds.AssetDomain;

namespace Wellcome.Dds.AssetDomainRepositories.Storage
{
    public class ArchiveStorageStoredFileInfo : IArchiveStorageStoredFileInfo
    {
        public ArchiveStorageStoredFileInfo(DateTime lastWriteTime, string uri, string relativePath)
        {
            LastWriteTime = lastWriteTime;
            Uri = uri;
            RelativePath = relativePath;
        }

        public DateTime LastWriteTime { get; }
        public string Uri { get; }
        public string RelativePath { get; }
    }
}
