using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IDigitisedManifestation : IDigitisedResource
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
        int SequenceIndex { get; set; }

        IPdf Pdf { get; }

        string DlcsStatus { get; set; }
        string DlcsResponse { get; set; }

        // See note in implementation - we want to move this method out of this interface, not the right place for it.
        AVDerviative[] GetAVDerivatives(
            string avDerivativeTemplateVideo,
            string avDerivativeTemplateAudio);
    }

}
