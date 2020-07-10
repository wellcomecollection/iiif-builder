using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Utils.Storage;
using Utils.Threading;

namespace Utils.Caching
{
    public class BinaryObjectCache<T> : IBinaryObjectCache<T> where T : class
    {
        private ILogger<BinaryObjectCache<T>> logger;
        private BinaryObjectCacheOptions options;
        private IStorage storage;
        private IMemoryCache memoryCache;
        private TimeSpan cacheDuration;

        private readonly AsyncKeyedLock asyncLocker = new AsyncKeyedLock();

        public BinaryObjectCache(
            ILogger<BinaryObjectCache<T>> logger,
            IOptions<BinaryObjectCacheOptions> options,
            IStorage storage,
            IMemoryCache memoryCache
            )
        {
            this.logger = logger;
            this.options = options.Value;
            this.storage = storage;
            this.storage.Container = this.options.Container; // not sure about setting this here
            this.memoryCache = memoryCache;
            cacheDuration = new TimeSpan(0, 0, this.options.MemoryCacheSeconds);
        }

        private string GetFileName(string key)
        {
            return key + ".ser";
        }

        public ISimpleStoredFileInfo GetCachedFile(string key)
        {
            string fileName = GetFileName(key);
            return storage.GetCachedFile(fileName);
        }

        public void DeleteCacheFile(string key)
        {
            if (memoryCache != null)
            {
                memoryCache.Remove(GetMemoryCacheKey(key));
            }
            string fileName = GetFileName(key);
            storage.DeleteCacheFile(fileName);
        }


        private string GetMemoryCacheKey(string key)
        {
            return options.Prefix + key;
        }


        public Task<T> GetCachedObject(string key, Func<T> getFromSource)
        {
            return GetCachedObject(key, getFromSource, null);
        }

        public async Task<T> GetCachedObject(string key, Func<T> getFromSource, Predicate<T> storedVersionIsStale)
        {
            T t = default(T);
            if (options.AvoidCaching)
            {
                if (getFromSource != null)
                {
                    t = getFromSource();
                    if (t != null && !options.AvoidSaving)
                    {
                        await storage.Write(t, GetCachedFile(key), options.WriteFailThrowsException);
                    }
                }
                return t;
            }
            var memoryCacheKey = GetMemoryCacheKey(key);
            var cachedFile = GetCachedFile(key);
            bool memoryCacheMiss = false;

            if (memoryCache != null)
            {
                t = memoryCache.Get(memoryCacheKey) as T;
            }
            if (t == null)
            {
                using (var processLock = await asyncLocker.LockAsync(String.Intern(key)))
                {
                    // check in memoryCache cache again
                    if (memoryCache != null)
                    {
                        t = memoryCache.Get(memoryCacheKey) as T;
                    }

                    if (t == null)
                    {
                        memoryCacheMiss = true;
                        if(logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Cache MISS for {0}, will attempt read from disk", memoryCacheKey);
                        }
                        t = await storage.Read<T>(cachedFile);
                    }
                    if (t != null && storedVersionIsStale != null && storedVersionIsStale(t))
                    {
                        t = null;
                    }
                    if (t == null)
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Disk MISS for {0}, will attempt read from source", memoryCacheKey);
                        }
                        if (getFromSource != null)
                        {
                            t = getFromSource();
                            if (t != null)
                            {
                                await storage.Write(t, cachedFile, options.WriteFailThrowsException);
                            }
                        }
                    }
                    if (t != null && memoryCacheMiss && memoryCache != null)
                    {
                        PutInMemoryCache(t, memoryCacheKey);
                    }
                }
            }
            return t;
        }
        
        public async Task<T> GetCachedObject(string key, Func<Task<T>> getFromSource, Predicate<T> storedVersionIsStale)
        {
            T t = default;
            if (options.AvoidCaching)
            {
                if (getFromSource != null)
                {
                    t = await getFromSource();
                    if (t != null && !options.AvoidSaving)
                    {
                        await storage.Write(t, GetCachedFile(key), options.WriteFailThrowsException);
                    }
                }
                return t;
            }
            var memoryCacheKey = GetMemoryCacheKey(key);
            var cachedFile = GetCachedFile(key);
            bool memoryCacheMiss = false;

            if (memoryCache != null)
            {
                t = memoryCache.Get(memoryCacheKey) as T;
            }
            if (t == null)
            {
                using (var processLock = await asyncLocker.LockAsync(String.Intern(key)))
                {
                    // check in memoryCache cache again
                    if (memoryCache != null)
                    {
                        t = memoryCache.Get(memoryCacheKey) as T;
                    }

                    if (t == null)
                    {
                        memoryCacheMiss = true;
                        if(logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Cache MISS for {0}, will attempt read from disk", memoryCacheKey);
                        }
                        t = await storage.Read<T>(cachedFile);
                    }
                    if (t != null && storedVersionIsStale != null && storedVersionIsStale(t))
                    {
                        t = null;
                    }
                    if (t == null)
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Disk MISS for {0}, will attempt read from source", memoryCacheKey);
                        }
                        if (getFromSource != null)
                        {
                            t = await getFromSource();
                            if (t != null)
                            {
                                await storage.Write(t, cachedFile, options.WriteFailThrowsException);
                            }
                        }
                    }
                    if (t != null && memoryCacheMiss && memoryCache != null)
                    {
                        PutInMemoryCache(t, memoryCacheKey);
                    }
                }
            }
            return t;
        }

        private void PutInMemoryCache(T t, string cacheKey)
        {
            if (options.MemoryCacheSeconds <= 0)
            {
                return;
            }
            memoryCache.Set(cacheKey, t, cacheDuration);
        }
    }
}
