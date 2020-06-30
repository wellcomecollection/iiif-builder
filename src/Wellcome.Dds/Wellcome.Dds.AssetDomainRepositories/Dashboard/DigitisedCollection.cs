using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Dashboard
{
    public class DigitisedCollection : BaseDigitisedResource, IDigitisedCollection
    {
        public ICollection MetsCollection { get; set; }
        public IEnumerable<IDigitisedManifestation> Manifestations { get; set; }
        public IEnumerable<IDigitisedCollection> Collections { get; set; }
    }
}
