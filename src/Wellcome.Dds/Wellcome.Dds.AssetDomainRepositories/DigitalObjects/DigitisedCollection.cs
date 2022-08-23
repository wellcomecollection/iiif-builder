using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.DigitalObjects
{
    public class DigitisedCollection : BaseDigitalObject, IDigitisedCollection
    {
        public ICollection MetsCollection { get; set; }
        public IEnumerable<IDigitalManifestation> Manifestations { get; set; }
        public IEnumerable<IDigitisedCollection> Collections { get; set; }
    }
}
