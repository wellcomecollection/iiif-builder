using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Threading
{
    public sealed class AsyncKeyedLock
    {
        public IDisposable Lock(object key)
        {
            GetOrCreate(key).Wait();
            return new Releaser(key);
        }

        public async Task<IDisposable> LockAsync(object key)
        {
            await GetOrCreate(key).WaitAsync();
            return new Releaser(key);
        }
        
        public async Task<IDisposable> LockAsync(object key, TimeSpan timeout, bool throwIfNoLock = false)
        {
            var success = await GetOrCreate(key).WaitAsync(timeout);
            if (!success && throwIfNoLock)
            {
                throw new TimeoutException(
                    $"Unable to attain lock for {key} within timeout of {timeout.TotalMilliseconds}ms");
            }

            return new Releaser(key) { HaveLock = success };
        }
        
        private SemaphoreSlim GetOrCreate(object key)
        {
            RefCounted<SemaphoreSlim>? item;
            lock (SemaphoreSlims)
            {
                if (SemaphoreSlims.TryGetValue(key, out item))
                {
                    ++item.RefCount;
                }
                else
                {
                    item = new RefCounted<SemaphoreSlim>(new SemaphoreSlim(1, 1));
                    SemaphoreSlims[key] = item;
                }
            }
            return item.Value;
        }
        
        private sealed class RefCounted<T>
        {
            public RefCounted(T value)
            {
                RefCount = 1;
                Value = value;
            }

            public int RefCount { get; set; }
            public T Value { get; }
        }

        private static readonly Dictionary<object, RefCounted<SemaphoreSlim>> SemaphoreSlims = new();

        public sealed class Releaser : IDisposable
        {
            public Releaser(object key)
            {
                Key = key;
            }
            
            public object Key { get; set; }

            public bool HaveLock { get; set; } = true;

            public void Dispose()
            {
                if (!HaveLock)
                {
                    return;
                }
                
                RefCounted<SemaphoreSlim> item;
                lock (SemaphoreSlims)
                {
                    item = SemaphoreSlims[Key];
                    --item.RefCount;
                    if (item.RefCount == 0)
                        SemaphoreSlims.Remove(Key);
                }
                item.Value.Release();
            }
        }
    }
}