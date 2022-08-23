using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    public interface IDigitisedCollection : IDigitalObject
    {
        ICollection MetsCollection { get; set; }
        IEnumerable<IDigitalManifestation> Manifestations { get; set; }
        IEnumerable<IDigitisedCollection> Collections { get; set; }
    }
}
