using System;
using Utils.Storage;

namespace Utils.Caching
{
    public interface IBinaryObjectCache<T> where T : class
    {
        T GetCachedObject(string key, Func<T> getFromSource);
        T GetCachedObject(string key, Func<T> getFromSource, Predicate<T> storedVersionIsStale);

        ISimpleStoredFileInfo GetCachedFile(string key);
        void DeleteCacheFile(string key);
    }
}
