using System;

namespace Utils.Storage
{
    public interface ISimpleStoredFileInfo
    {
        DateTime? LastWriteTime { get; }
        
        string Uri { get; }
        
        bool Exists { get; }
        
        // add folder/key idea here...
        public string Container { get; }
        
        public string Path { get; }
    }
}
