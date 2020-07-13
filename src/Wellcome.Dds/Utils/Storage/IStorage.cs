using System.Threading.Tasks;

namespace Utils.Storage
{
    public interface IStorage
    {
        // This might be a bucket, or a filesystem location
        public string Container { get; set; }

        // TODO - would this be better named GetCachedFileInfo?
        ISimpleStoredFileInfo GetCachedFile(string fileName);
        Task DeleteCacheFile(string fileName);
        Task Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class;
        Task<T> Read<T>(ISimpleStoredFileInfo fileInfo) where T : class;
    }
}
