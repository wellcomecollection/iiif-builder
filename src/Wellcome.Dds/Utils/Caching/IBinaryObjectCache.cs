using System;
using System.Threading.Tasks;
using Utils.Storage;

namespace Utils.Caching
{
    public interface IBinaryObjectCache<T> where T : class
    {
        Task<T> GetCachedObject(string key, Func<T> getFromSource);
        Task<T> GetCachedObject(string key, Func<T> getFromSource, Predicate<T> storedVersionIsStale);

        ISimpleStoredFileInfo GetCachedFile(string key);
        void DeleteCacheFile(string key);
    }
}
