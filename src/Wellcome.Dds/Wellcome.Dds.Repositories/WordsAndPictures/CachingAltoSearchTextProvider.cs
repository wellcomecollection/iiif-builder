using System;
using System.Threading.Tasks;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.WordsAndPictures;

namespace Wellcome.Dds.Repositories.WordsAndPictures
{
    public class CachingAltoSearchTextProvider : ISearchTextProvider
    {
        private readonly AltoSearchTextProvider altoSearchTextProvider;
        private readonly IBinaryObjectCache<Text> searchTextCache; 
        
        public CachingAltoSearchTextProvider(
            AltoSearchTextProvider altoSearchTextProvider,
            IBinaryObjectCache<Text> searchTextCache)
        {
            this.altoSearchTextProvider = altoSearchTextProvider;
            this.searchTextCache = searchTextCache;
        }

        public Task<Text?> GetSearchText(string identifier)
        {
            return searchTextCache.GetCachedObject(identifier, null, t => false);
        }

        public Task<Text> ForceSearchTextRebuild(string identifier)
        {
            Func<Task<Text>> getFromSource = () => altoSearchTextProvider.GetSearchText(identifier);
            return searchTextCache.GetCachedObjectFromLocal(identifier, getFromSource);
        }

        public ISimpleStoredFileInfo GetFileInfo(string identifier)
        {
            return searchTextCache.GetCachedFile(identifier);
        }

        public void DeleteAltoCacheFile(string identifier)
        {
            searchTextCache.DeleteCacheFile(identifier);
        }
        
        public void DisableMemoryCache()
        {
            searchTextCache.DisableMemoryCache();
        }

        public void EnableMemoryCache()
        {
            searchTextCache.EnableMemoryCache();
        }
    }
}
