namespace Utils.Caching
{
    public class BinaryObjectCacheOptions
    {
        public bool AvoidCaching { get; set; }
        public bool AvoidSaving { get; set; }
        public bool WriteFailThrowsException { get; set; }
        public string Container { get; set; }
        public string Prefix { get; set; }
        public int MemoryCacheSeconds { get; set; }
    }
}
