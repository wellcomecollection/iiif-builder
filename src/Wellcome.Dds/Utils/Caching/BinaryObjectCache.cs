﻿using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using Utils.Storage;

namespace Utils.Caching
{
    public class BinaryObjectCache<T> : IBinaryObjectCache<T> where T : class
    {
        private ILogger<BinaryObjectCache<T>> logger;
        private BinaryObjectCacheOptions options;
        private ICacheStorage storage;
        private IMemoryCache memoryCache;
        private TimeSpan cacheDuration;

        public BinaryObjectCache(
            ILogger<BinaryObjectCache<T>> logger,
            IOptions<BinaryObjectCacheOptions> options,
            ICacheStorage storage,
            IMemoryCache memoryCache
            )
        {
            this.logger = logger;
            this.options = options.Value;
            this.storage = storage;
            this.storage.Folder = this.options.Folder; // not sure about setting this here
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


        public T GetCachedObject(string key, Func<T> getFromSource)
        {
            return GetCachedObject(key, getFromSource, null);
        }

        public T GetCachedObject(string key, Func<T> getFromSource, Predicate<T> storedVersionIsStale)
        {
            T t = default(T);
            if (options.AvoidCaching)
            {
                if (getFromSource != null)
                {
                    t = getFromSource();
                    if (t != null && !options.AvoidSaving)
                    {
                        storage.Write(t, GetCachedFile(key), options.WriteFailThrowsException);
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
                lock (String.Intern(key))
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
                        t = storage.Read<T>(cachedFile);
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
                                storage.Write(t, cachedFile, options.WriteFailThrowsException);
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
