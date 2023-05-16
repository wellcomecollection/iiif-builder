using System.Collections.Generic;

namespace Utils.Caching
{
    public class BinaryObjectCacheOptionsByType : Dictionary<string, BinaryObjectCacheOptions> { }

    public class BinaryObjectCacheOptions
    {
        #nullable disable
        
        public bool AvoidCaching { get; set; }
        public bool AvoidSaving { get; set; }
        public bool WriteFailThrowsException { get; set; }
        public string Container { get; set; }
        public string Prefix { get; set; }
        public int MemoryCacheSeconds { get; set; }

        /// <summary>
        /// How long to wait to get critical path lock (ms)
        /// </summary>
        public int CriticalPathTimeout { get; set; } = 1000;

        /// <summary>
        /// Whether to throw an exception if critical path lock times out
        /// </summary>
        public bool ThrowOnCriticalPathTimeout { get; set; } = false;
    }
}
