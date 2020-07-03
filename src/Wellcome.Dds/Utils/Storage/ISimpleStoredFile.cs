using System;

namespace Utils.Storage
{
    public interface ISimpleStoredFileInfo
    {
        DateTime LastWriteTime { get; }
        string Uri { get; }
        bool Exists { get; }
    }
}
