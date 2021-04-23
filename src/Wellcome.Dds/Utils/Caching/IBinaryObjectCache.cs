using System;
using System.Threading.Tasks;
using Utils.Storage;

namespace Utils.Caching
{
    public interface IBinaryObjectCache<T> where T : class
    {
        Task<T> GetCachedObject(string key, Func<Task<T>> getFromSource);

        Task<T> GetCachedObject(string key, Func<Task<T>> getFromSource, Predicate<T> storedVersionIsStale);

        /// <summary>
        /// Get cached object from local cache only, do not attempt to read from backing store.
        /// </summary> 
        Task<T> GetCachedObjectFromLocal(string key, Func<Task<T>> getFromSource);

        ISimpleStoredFileInfo GetCachedFile(string key);

        Task DeleteCacheFile(string key);
    }
}
