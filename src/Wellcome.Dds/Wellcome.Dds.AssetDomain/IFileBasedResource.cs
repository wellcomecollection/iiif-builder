using Utils.Storage;

namespace Wellcome.Dds.AssetDomain
{
    public interface IFileBasedResource
    {
        IArchiveStorageStoredFileInfo SourceFile { get; set; }
    }
}
