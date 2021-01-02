using Utils.Storage;

namespace Wellcome.Dds.AssetDomain.Mets
{
    /// <summary>
    /// This is like IPhysicalFile, but for files not mentioned in structmaps.
    /// It's used for OLD Poster Images as a temporary measure, until the format of
    /// METS for poster image can be determined.
    /// </summary>
    public interface IStoredFile
    {
        IWorkStore WorkStore { get; set; }
        IAssetMetadata AssetMetadata { get; set; }
        string RelativePath { get; set; }
        IArchiveStorageStoredFileInfo GetStoredFileInfo();
    }
}
