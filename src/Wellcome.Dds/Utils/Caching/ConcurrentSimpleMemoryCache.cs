using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;

namespace Utils.Caching
{
    public class ConcurrentSimpleMemoryCache : ISimpleCache
    {
        /// <summary>
        /// An implementation of ISimpleCache that uses the ASP.NET runtime cache directly.
        /// It prevents multiple threads running the cache rebuilding code at the same time.
        /// It does not avoid the problem of a stale cache hit incurring rebuild time.
        /// 
        /// For that, use SuperCache. This class shouldn't be used for expensive-to-build objects.
        /// </summary>

        ILogger<ConcurrentSimpleMemoryCache> logger;
        IMemoryCache memoryCache;

        public ConcurrentSimpleMemoryCache(
            ILogger<ConcurrentSimpleMemoryCache> logger,
            IMemoryCache memoryCache
        )
        {
            this.logger = logger;
            this.memoryCache = memoryCache;
        }

        private static readonly object RealCacheLock = new object();

        public TResult GetCached<TResult>(int maxAgeSeconds, string cacheKey, Func<TResult> createObject)
            where TResult : class
        {
            var cachedObject = memoryCache.Get(cacheKey) as TResult;
            if (cachedObject == null)
            {
                logger.LogInformation("Cache miss for key \"" + cacheKey + "\", type: " + typeof(TResult));
                logger.LogDebug("caller is {0}", createObject.Method.Name);
                logger.LogDebug("Will get a lock to avoid concurrent access");
                lock (RealCacheLock)
                {
                    logger.LogDebug("Have entered lock for " + cacheKey);
                    // make sure it hasn't been rebuilt while we were waiting to acquire the lock
                    cachedObject = memoryCache.Get(cacheKey) as TResult;
                    if (cachedObject == null)
                    {
                        logger.LogInformation("No object recoverable from cache OR stale backup for key \""
                            + cacheKey + "\", will have to keep the user waiting while we rebuild.");
                        cachedObject = CreateAndCache(cacheKey, createObject, maxAgeSeconds);
                    }
                }
            }
            return cachedObject;
        }

        private TResult CreateAndCache<TResult>(string cacheKey, Func<TResult> createObject, int maxAgeSeconds) where TResult : class
        {
            var cachedObject = createObject();
            Insert(cacheKey, cachedObject, maxAgeSeconds);
            return cachedObject;
        }

        private void Insert<TResult>(string cacheKey, TResult cachedObject, int maxAgeSeconds) where TResult : class
        {
            if (cachedObject == null)
            {
                logger.LogWarning("Attempt to create new object for caching returned null. Cache key: " + cacheKey + ", type: " + typeof(TResult));
            }
            else
            {
                if (cacheKey.Contains(KnownIdentifiers.ChemistAndDruggist))
                {
                    memoryCache.Set(cacheKey, cachedObject);
                    return;
                }
                memoryCache.Set(cacheKey, cachedObject, new TimeSpan(0, 0, maxAgeSeconds));
            }
        }

        public void Remove(string key)
        {
            memoryCache.Remove(key);
        }

    }
}
