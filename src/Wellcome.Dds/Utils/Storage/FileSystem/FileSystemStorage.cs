using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace Utils.Storage.FileSystem
{
    public class FileSystemStorage : IStorage
    {
        private readonly ILogger<FileSystemStorage> logger;

        public FileSystemStorage(ILogger<FileSystemStorage> logger)
        {
            this.logger = logger;
        }

        public string Container { get; set; }

        public ISimpleStoredFileInfo GetCachedFileInfo(string fileName)
        {
            string cachedFilePath = Path.Combine(Container, fileName);
            FileInfo fi = new FileInfo(cachedFilePath);
            return new FileSystemStoredFileInfo(fi);
        }
        
        public Task DeleteCacheFile(string fileName)
        {
            string cachedFilePath = Path.Combine(Container, fileName);
            File.Delete(cachedFilePath);
            return Task.CompletedTask;
        }

        public async Task<T> Read<T>(ISimpleStoredFileInfo fileInfo) where T : class
        {
            if (!fileInfo.Exists) return null;
            T t = default(T);
            try
            {
                IFormatter formatter = new BinaryFormatter();
                await using (Stream stream = new FileStream(fileInfo.Uri, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    stream.Position = 0;
                    t = formatter.Deserialize(stream) as T;
                    stream.Close();
                }
                if (t == null)
                {
                    logger.LogError("Attempt to deserialize '" + fileInfo.Uri + "' from disk failed.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Attempt to deserialize '" + fileInfo.Uri + "' from disk failed", ex);
            }
            return t;
        }

        public async Task Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class
        {
            logger.LogInformation("Writing cache file '" + fileInfo.Uri + "' to disk");
            try
            {
                File.Delete(fileInfo.Uri);
                IFormatter formatter = new BinaryFormatter();
                await using (Stream stream = new FileStream(fileInfo.Uri, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    formatter.Serialize(stream, t);
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Unable to write to file '" + fileInfo.Uri + "'", ex);
                if (writeFailThrowsException)
                {
                    throw;
                }
            }
        }

        
    }
}
