using System;
using System.IO;
using System.Threading.Tasks;

namespace Utils.Storage.FileSystem
{
    public class FileSystemStoredFileInfo : ISimpleStoredFileInfo
    {
        private readonly FileInfo fileInfo;

        public FileSystemStoredFileInfo(FileInfo fileInfo)
        {
            this.fileInfo = fileInfo;
        }

        public string Uri => fileInfo.FullName;
        public Task<bool> DoesExist() => Task.FromResult(fileInfo.Exists);

        public Task<DateTime?> GetLastWriteTime() => Task.FromResult<DateTime?>(fileInfo.LastWriteTime);
        
        public string? Container => fileInfo.DirectoryName;

        public string Path => fileInfo.Name;
    }
}