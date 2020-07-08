using System;
using System.IO;

namespace Utils.Storage.FileSystem
{
    public class FileSystemStoredFileInfo : ISimpleStoredFileInfo
    {
        private readonly FileInfo fileInfo;

        public FileSystemStoredFileInfo(FileInfo fileInfo)
        {
            this.fileInfo = fileInfo;
        }

        public DateTime LastWriteTime => fileInfo.LastWriteTime;

        public string Uri => fileInfo.FullName;

        public bool Exists => fileInfo.Exists;

        public string Container => fileInfo.DirectoryName;

        public string Path => fileInfo.Name;
    }
}