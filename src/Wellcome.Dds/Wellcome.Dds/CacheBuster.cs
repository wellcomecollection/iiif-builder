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
            throw new NotImplementedException();
        }

        public ISimpleStoredFileInfo GetAltoSearchTextCacheFileInfo(string bNumber, int sequenceIndex)
        {
            throw new NotImplementedException();
        }

        public ISimpleStoredFileInfo GetAllAnnotationsCacheFileInfo(string bNumber, int sequenceIndex)
        {
            throw new NotImplementedException();
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
}
