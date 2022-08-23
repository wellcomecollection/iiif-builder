using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Storage.FileSystem
{
    /// <summary>
    /// Simple implementation for testing purposes. Assumes all b numbers share a common root directory
    ///  - the METS (and if being tested, assets such as JP2s) all live in the same root directory alongside each other.
    /// </summary>
    public class FileSystemWorkStorageFactory : IWorkStorageFactory
    {
        private readonly string rootDirectory;
        
        public FileSystemWorkStorageFactory(string rootDirectory)
        {
            this.rootDirectory = rootDirectory;
        }
        
        public Task<IWorkStore> GetWorkStore(DdsIdentifier identifier)
        {
            return Task.FromResult<IWorkStore>(new FileSystemWorkStore(rootDirectory, identifier));
        }
    }
}