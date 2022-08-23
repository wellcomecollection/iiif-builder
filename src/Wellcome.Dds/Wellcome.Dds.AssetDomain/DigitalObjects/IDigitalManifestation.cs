using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    public interface IDigitalManifestation : IDigitalObject
    {
        IManifestation MetsManifestation { get; set; }

        /// <summary>
        /// The images held by the DLCS that match the metadata for this manifestation
        /// based on string3
        /// </summary>
        IEnumerable<Image> DlcsImages { get; set; }
        bool JobExactMatchForManifestation(DlcsIngestJob job);

        /// <summary>
        /// TODO: This doesn't belong here! Only here for PDF link to work and be same as in IIIF manifest
        /// </summary>
        // int SequenceIndex { get; set; }

        string DlcsStatus { get; set; }
        string DlcsResponse { get; set; }

        IPdf PdfControlFile { get; set; }
    }
}
