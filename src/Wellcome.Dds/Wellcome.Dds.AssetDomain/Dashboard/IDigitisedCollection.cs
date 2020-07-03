using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain.Dashboard
{
    public interface IDigitisedCollection : IDigitisedResource
    {
        ICollection MetsCollection { get; set; }
        IEnumerable<IDigitisedManifestation> Manifestations { get; set; }
        IEnumerable<IDigitisedCollection> Collections { get; set; }
    }
}
