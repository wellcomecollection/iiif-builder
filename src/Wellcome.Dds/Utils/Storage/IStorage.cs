using System.IO;
using System.Threading.Tasks;

namespace Utils.Storage
{
    /// <summary>
    /// Interface for storing serialized representations of cached files.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// This might be a bucket, or a filesystem location dependent on implementation. 
        /// </summary>
        //public string Container { get; set; }
        
        ISimpleStoredFileInfo GetCachedFileInfo(string container, string fileName);
        
        Task DeleteCacheFile(string container, string fileName);
        
        Task Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class;
        
        /// <summary>
        /// Read object represented by <see cref="ISimpleStoredFileInfo"/>
        /// </summary>
        Task<T?> Read<T>(ISimpleStoredFileInfo fileInfo) where T : class;

        Task<Stream?> GetStream(string container, string fileName);
    }
}
