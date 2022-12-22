using System;
using System.Collections.Generic;
using Utils.Storage;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace Wellcome.Dds.AssetDomain.Mets
{
    /// <summary>
    /// Represents an entry in the METS physical files list.
    /// Until 2021, this corresponded to an actual file, on disk or in the storage bag, with ONE
    /// exception - the ALTO file as an "adjunct" file.
    ///
    /// With the new AV workflow, we have more support for MULTIPLE real files for one IPhysicalFile.
    /// But in many cases consumers will still treat this as the way of getting to the access file.
    /// </summary>
    public interface IPhysicalFile
    {
        IWorkStore WorkStore { get; set; }
        string Id { get; set; }
        string Type { get; set; }
        
        // Added for born digital
        
        // From PREMIS, in Archivematica METS contains a full path.
        string OriginalName { get; set; }
        
        DateTime? CreatedDate { get; set; }
        
        /// <summary>
        /// Order from METS, not necessarily given
        /// </summary>
        int? Order { get; set; }
        /// <summary>
        /// Order after sorting by Order, always has a value
        /// </summary>
        int Index { get; set; }
        string OrderLabel { get; set; }

        //Guid SdbId { get; set; }
        string? StorageIdentifier { get; set; }
        
        string? MimeType { get; set; }
        IAssetMetadata AssetMetadata { get; set; }
        string AccessCondition { get; set; }
        
        /// <summary>
        /// The path of the ACCESS file (e.g., JP2, MP4)
        /// </summary>
        string RelativePath { get; set; }
        
        /// <summary>
        /// Gets path to ALTO file associated with this file, if it has one.
        /// </summary>
        string RelativeAltoPath { get; set; }

        AssetFamily Family { get; set; }

        // Consider replacing these by using the Files property.
        IArchiveStorageStoredFileInfo GetStoredFileInfo();
        IArchiveStorageStoredFileInfo GetStoredAltoFileInfo();
        
        List<IStoredFile> Files { get; set; }
        string RelativePosterPath { get; set; }
        string RelativeTranscriptPath { get; set; }
        string RelativeMasterPath { get; set; }
    }
}
