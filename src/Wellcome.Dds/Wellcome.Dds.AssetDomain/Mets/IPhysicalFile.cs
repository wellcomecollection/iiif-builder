﻿using System;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IPhysicalFile
    {
        IWorkStore WorkStore { get; set; }
        string Id { get; set; }
        string Type { get; set; }
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
        string StorageIdentifier { get; set; }
        
        string MimeType { get; set; }
        IAssetMetadata AssetMetadata { get; set; }
        string AccessCondition { get; set; }
        string DzLicenseCode { get; set; }
        string RelativePath { get; set; }
        string RelativeAltoPath { get; set; }
        string ToStringWithDimensions();

        AssetFamily Family { get; set; }

        IStoredFileInfo GetStoredFileInfo();
        IStoredFileInfo GetStoredAltoFileInfo();
    }
}
