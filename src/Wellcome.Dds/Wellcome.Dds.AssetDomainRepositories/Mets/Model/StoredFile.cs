using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class StoredFile : IStoredFile
    {
        public IWorkStore WorkStore { get; set; }
        public IAssetMetadata AssetMetadata { get; set; }
        public string RelativePath { get; set; }
        public IArchiveStorageStoredFileInfo GetStoredFileInfo()
        {
            return WorkStore.GetFileInfoForPath(RelativePath);
        }
    }
}
