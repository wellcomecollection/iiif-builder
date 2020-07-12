using System;
using System.Threading.Tasks;
using Utils.Storage;

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

        public ISimpleStoredFileInfo GetAltoSearchTextCacheFileInfo(string bNumber, int sequenceIndex)
        {
            return new NonExistentFileInfo();
            //throw new NotImplementedException();
        }

        public ISimpleStoredFileInfo GetAllAnnotationsCacheFileInfo(string bNumber, int sequenceIndex)
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
