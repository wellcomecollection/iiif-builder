﻿using System;

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
        public string PackageIdentifier { get; set; } // the b number, aka String1
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
        public string PosterImage { get; set; }
        public string PermittedOperations { get; set; }
        public string RootSectionAccessCondition { get; set; }
        public string RootSectionType { get; set; }
        public string FirstFileName { get; set; }
        public string FirstFileStorageIdentifier { get; set; }
        public string FirstFileExtension { get; set; }
        public string DipStatus { get; set; }
        public string ErrorMessage { get; set; }
        public string PackageFile { get; set; }
        public DateTime? PackageFileModified { get; set; }
        public string ManifestationFile { get; set; }
        public DateTime? ManifestationFileModified { get; set; }
        public string AssetType { get; set; }
        public string DlcsAssetType { get; set; }
    }
}
