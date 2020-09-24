using System;

namespace Wellcome.Dds
{
    /// <summary>
    /// This is equivalent to FlatManifestation in old DDS.
    /// It's almost a IIIF Manifest row, but not quite.
    /// Still some rework to do before it can become "Manifest"
    /// </summary>
    public class Manifestation
    {
        // FM = FlatManifestation, see https://github.com/wellcomelibrary/dds-ecosystem/blob/new-storage-service/wellcome-dds/Wellcome.Dds.Data/FlatManifestation.cs
        
        public string Id { get; set; } // this is string3, the same as the manifest in S3; bnnn_0003
        
        /// <summary>
        /// Gets or sets the bNumber for this work (e.g. b18031511)
        /// </summary>
        public string PackageIdentifier { get; set; } // the b number, aka String1
        
        public string WorkId { get; set; }
        
        // Going to store both these forms for now, until we know which one is best to use (or if we can avoid storing it at all!
        public string CalmRef { get; set; } // Only for archive items
        public string CalmRefParent { get; set; } // Only for archive items
        
        /// <summary>
        /// // This is the same as ReferenceNumber for archives
        /// </summary>
        public string CalmAltRef { get; set; } // Only for archive items
        
        public string CalmAltRefParent { get; set; } // Only for archive items
        
        /// <summary>
        /// The top level of this item's archive tree
        /// </summary>
        public string CollectionReferenceNumber { get; set; } // Only for archive items
        
        /// <summary>
        /// Eventually, all works will have one of these, but for now it's just archives
        /// </summary>
        public string ReferenceNumber { get; set; } // The same as CalmAltRef for archives, may be
        
        /// <summary>
        /// Gets or sets the individual identifier for this volume (e.g. b18031511_0001)
        /// </summary>
        public string ManifestationIdentifier { get; set; } // do we need this? Same as Id (String3)
        public string VolumeIdentifier { get; set; } // legacy of Old model - aka String2
        public DateTime Processed { get; set; }
        public int PackageShortBNumber { get; set; }
        public string Label { get; set; } // RootSectionTitle in FM?
        public string PackageLabel { get; set; } // RootSectionTitle in FM?  (which?) - allows us to have Manifest and Work Labels

        public int Index { get; set; } // Manifestation in FM
        public int FileCount { get; set; }
        
        
        public bool SupportsSearch { get; set; }
        public bool IsAllOpen { get; set; }
        
        // Consider adding
        //public bool ContainsRestrictedFiles { get; set; }
        
        /// <summary>
        /// Image service for thumbnail obtained from Catalogue Record, which might
        /// differ from the first image (e.g., Title page)
        /// </summary>
        public string CatalogueThumbnail { get; set; }
        
        /// <summary>
        /// Serialised array of [w,h] pairs, starting with the actual size, then
        /// available thumbs, descending
        /// </summary>
        public string CatalogueThumbnailDimensions { get; set; }
        
        /// <summary>
        /// Image Service for first file thumbnail
        /// </summary>
        public string FirstFileThumbnail { get; set; }
        
        /// <summary>
        /// Serialised array of [w,h] pairs, starting with the actual size, then
        /// available thumbs, descending
        /// </summary>
        public string FirstFileThumbnailDimensions { get; set; }
        
        public string WorkType { get; set; }
        public string PermittedOperations { get; set; }
        public string RootSectionAccessCondition { get; set; }
        public string RootSectionType { get; set; }
        public string FirstFileStorageIdentifier { get; set; }
        public string FirstFileExtension { get; set; }
        public string DipStatus { get; set; }
        public string PackageFile { get; set; }
        public DateTime? PackageFileModified { get; set; }
        public string ManifestationFile { get; set; }
        public DateTime? ManifestationFileModified { get; set; }
        public string AssetType { get; set; }
        public string DlcsAssetType { get; set; }
    }
}
