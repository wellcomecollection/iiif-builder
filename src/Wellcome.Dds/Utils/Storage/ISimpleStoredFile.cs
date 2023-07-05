using System;
using System.Threading.Tasks;

namespace Utils.Storage
{
    public interface ISimpleStoredFileInfo
    {
        string Uri { get; }
        
        Task<bool> DoesExist();

        Task<DateTime?> GetLastWriteTime();
        
        // add folder/key idea here...
        public string? Container { get; }
        
        public string Path { get; } 
        
        public long? Size { get; }
    }
}
