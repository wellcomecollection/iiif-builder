using System;
using System.Threading.Tasks;
using Utils.Storage;

namespace Utils.Caching
{
    public interface IBinaryObjectCache<T> where T : class
    {
        Task<T> GetCachedObject(string key, Func<Task<T>> getFromSource);

        Task<T> GetCachedObject(string key, Func<Task<T>> getFromSource, Predicate<T> storedVersionIsStale);

        ISimpleStoredFileInfo GetCachedFile(string key);

        Task DeleteCacheFile(string key);
    }
}
