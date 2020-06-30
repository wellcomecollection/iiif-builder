using System;
using Wellcome.Dds.AssetDomain;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class ArchiveStorageStoredFileInfo : IStoredFileInfo
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
