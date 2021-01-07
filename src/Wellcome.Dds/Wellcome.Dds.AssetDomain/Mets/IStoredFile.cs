using Utils.Storage;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace Wellcome.Dds.AssetDomain.Mets
{
    /// <summary>
    /// Represents an actual file in storage (rather than an IPhysicalFile which represents an
    /// entry in the METS Physical File list and might point to more than one IStoredFile).
    ///
    /// In the migration/stopgap PosterImage implementation, a PosterImage is an IStoredFile without any entry
    /// in the METS physical files, only a technical metadata entry.
    /// In the new AV workflow, MXF, Posters and Transcripts are all stored files too.
    /// </summary>
    public interface IStoredFile
    {
        IWorkStore WorkStore { get; set; }
        
        /// <summary>
        /// The technical metadata about the file included in the techMD section of the METS
        /// ALTO files won't have any technical metadata.
        /// </summary>
        IAssetMetadata AssetMetadata { get; set; }
        
        /// <summary>
        /// Location relative to the METS file
        /// </summary>
        string RelativePath { get; set; }
        IArchiveStorageStoredFileInfo GetStoredFileInfo();
        
        // New to support AV workflow
        
        /// <summary>
        /// The mets:file ID.
        /// This will be null for legacy poster images.
        /// </summary>
        string Id { get; set; }
        public string StorageIdentifier { get; set; }
        string MimeType { get; set; }
        
        /// <summary>
        /// e.g., ACCESS, POSTER, etc.
        /// This will only be declared in
        /// </summary>
        string Use { get; set; }
        public AssetFamily Family { get; set; }
    }
}
