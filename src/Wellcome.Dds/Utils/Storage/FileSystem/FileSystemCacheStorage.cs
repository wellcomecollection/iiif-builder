using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Utils.Storage.FileSystem
{
    public class FileSystemCacheStorage : ICacheStorage
    {
        private ILogger<FileSystemCacheStorage> logger;

        public FileSystemCacheStorage(ILogger<FileSystemCacheStorage> logger)
        {
            this.logger = logger;
        }

        public string Folder { get; set; }

        public ISimpleStoredFileInfo GetCachedFile(string fileName)
        {
            string cachedFilePath = Path.Combine(Folder, fileName);
            FileInfo fi = new FileInfo(cachedFilePath);
            return new FileSystemStoredFileInfo(fi);
        }
        
        public void DeleteCacheFile(string fileName)
        {
            string cachedFilePath = Path.Combine(Folder, fileName);
            File.Delete(cachedFilePath);
        }

        public T Read<T>(ISimpleStoredFileInfo fileInfo) where T : class
        {
            T t = default(T);
            if (fileInfo.Exists)
            {
                try
                {
                    IFormatter formatter = new BinaryFormatter();
                    using (Stream stream = new FileStream(fileInfo.Uri, FileMode.Open, FileAccess.Read, FileShare.Read))
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
            }
            return t;
        }

        public void Write<T>(T t, ISimpleStoredFileInfo fileInfo, bool writeFailThrowsException) where T : class
        {
            logger.LogInformation("Writing cache file '" + fileInfo.Uri + "' to disk");
            try
            {
                File.Delete(fileInfo.Uri);
                IFormatter formatter = new BinaryFormatter();
                using (Stream stream = new FileStream(fileInfo.Uri, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
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
