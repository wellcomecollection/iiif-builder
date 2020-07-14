using System;
using System.Threading.Tasks;
using Utils.Storage;
using Wellcome.Dds.Common;

namespace Wellcome.Dds
{
    [Obsolete("Needs Attention", false)]
    public class CacheBuster
    {
        public ISimpleStoredFileInfo GetPackageCacheFileInfo(string bNumber)
        {
            return new NonExistentFileInfo();
            //throw new NotImplementedException();
        }

        public ISimpleStoredFileInfo GetAltoSearchTextCacheFileInfo(DdsIdentifier ddsId)
        {
            return new NonExistentFileInfo();
            //throw new NotImplementedException();
        }

        public ISimpleStoredFileInfo GetAllAnnotationsCacheFileInfo(DdsIdentifier ddsId)
        {
            return new NonExistentFileInfo();
            //throw new NotImplementedException();
        }

        public CacheBustResult BustPackage(string bNumber)
        {
            throw new NotImplementedException();
        }

        public CacheBustResult BustAltoSearchText(string bNumber, Task<int> seqIndex)
        {
            throw new NotImplementedException();
        }

        public CacheBustResult BustAllAnnotations(string bNumber, Task<int> seqIndex)
        {
            throw new NotImplementedException();
        }
    }

    public class NonExistentFileInfo : ISimpleStoredFileInfo
    {
        public DateTime LastWriteTime => DateTime.MinValue;

        public string Uri => "nnn://non-existent-container/non-existent-path";

        public bool Exists => false;

        public string Container => "non-existent-container";

        public string Path => "non-existent-path";
    }
}
