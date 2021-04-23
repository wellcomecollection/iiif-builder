using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Utils.Storage;
using Utils.Threading;

namespace Utils.Caching
{
    public class BinaryObjectCache<T> : IBinaryObjectCache<T>
        where T : class
    {
        private readonly ILogger<BinaryObjectCache<T>> logger;
        private readonly BinaryObjectCacheOptions options;
        private readonly IStorage storage;
        private readonly IMemoryCache memoryCache;
        private readonly TimeSpan cacheDuration;

        private readonly AsyncKeyedLock asyncLocker = new();

        public BinaryObjectCache(
            ILogger<BinaryObjectCache<T>> logger,
            IOptions<BinaryObjectCacheOptionsByType> binaryObjectCacheOptionsByType,
            IStorage storage,
            IMemoryCache memoryCache
            )
        {
            this.logger = logger;
            options = binaryObjectCacheOptionsByType.Value[typeof(T).FullName];
            this.storage = storage;
            this.memoryCache = memoryCache;
            cacheDuration = TimeSpan.FromSeconds(this.options.MemoryCacheSeconds);
        }

        public ISimpleStoredFileInfo GetCachedFile(string key)
        {
            string fileName = GetFileName(key);
            return storage.GetCachedFileInfo(options.Container, fileName);
        }

        public Task DeleteCacheFile(string key)
        {
            memoryCache?.Remove(GetMemoryCacheKey(key));
            
            string fileName = GetFileName(key);
            
            // TODO - handle failure?
            return storage.DeleteCacheFile(options.Container, fileName);
        }

        public Task<T> GetCachedObject(string key, Func<Task<T>> getFromSource) 
            => GetCachedObject(key, getFromSource, null);

        public async Task<T> GetCachedObject(string key, Func<Task<T>> getFromSource, Predicate<T> storedVersionIsStale)
        {
            T t = default;
            if (options.AvoidCaching)
            {
                if (getFromSource == null) return t;
                t = await getFromSource();

                if (t != null && !options.AvoidSaving)
                {
                    var simpleStoredFileInfo = GetCachedFile(key);
                    await storage.Write(t, simpleStoredFileInfo, options.WriteFailThrowsException);
                }

                return t;
            }

            var memoryCacheKey = GetMemoryCacheKey(key);

            if (memoryCache != null)
            {
                t = memoryCache.Get(memoryCacheKey) as T;
            }

            if (t != null) return t;

            bool memoryCacheMiss = false;
            var cachedFile = GetCachedFile(key);
            
            using (var processLock = await GetLock(key))
            {
                // check in memoryCache cache again
                if (memoryCache != null)
                {
                    t = memoryCache.Get(memoryCacheKey) as T;
                }

                if (t == null)
                {
                    memoryCacheMiss = true;
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Cache MISS for {MemoryCacheKey}, will attempt read from disk", memoryCacheKey);
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
                        logger.LogDebug("Disk MISS for {MemoryCacheKey}, will attempt read from source", memoryCacheKey);
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

            return t;
        }

        // NOTE(DG) - this should be .bin when we move everything to protobuf
        private static string GetFileName(string key) => $"{key}.ser";

        private string GetMemoryCacheKey(string key) => string.Concat(options.Prefix, key);

        private void PutInMemoryCache(T t, string cacheKey)
        {
            if (options.MemoryCacheSeconds <= 0)
            {
                return;
            }

            if (cacheKey.Contains(KnownIdentifiers.ChemistAndDruggist))
            {
                memoryCache.Set(cacheKey, t);
                return;
            }
            memoryCache.Set(cacheKey, t, cacheDuration);
        }

        private Task<IDisposable> GetLock(string key)
            => asyncLocker.LockAsync(
                string.Intern(key), 
                TimeSpan.FromMilliseconds(options.CriticalPathTimeout),
                options.ThrowOnCriticalPathTimeout);
    }
}
