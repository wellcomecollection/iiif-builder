using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.DigitalObjects
{
    public class DigitalManifestation : BaseDigitalObject, IDigitalManifestation
    {
        public DigitalManifestation(IManifestation metsManifestation)
        {
            MetsManifestation = metsManifestation;
        }
        
        public IManifestation MetsManifestation { get; }
        
        /// <summary>
        /// The images already on the DLCS for this manifestation
        /// </summary>
        public IEnumerable<Image>? DlcsImages { get; set; }

        public IPdf? PdfControlFile { get; set; }

        public string? DlcsStatus { get; set; }
        public string? DlcsResponse { get; set; }
    }
}
