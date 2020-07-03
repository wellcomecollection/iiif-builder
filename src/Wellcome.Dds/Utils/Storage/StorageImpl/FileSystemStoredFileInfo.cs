using System;
using System.IO;
using Utils.Storage;

public class FileSystemStoredFileInfo : ISimpleStoredFileInfo
{
    private readonly FileInfo fileInfo;

    public FileSystemStoredFileInfo(FileInfo fileInfo)
    {
        this.fileInfo = fileInfo;
    }

    public DateTime LastWriteTime
    {
        get { return fileInfo.LastWriteTime; }
    }

    public string Uri
    {
        get { return fileInfo.FullName; }
    }

    public bool Exists
    { 
        get { return fileInfo.Exists; }
    }
}