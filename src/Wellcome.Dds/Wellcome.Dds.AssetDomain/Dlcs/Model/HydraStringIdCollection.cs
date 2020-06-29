using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class HydraStringIdCollection : HydraCollectionBase
    {
        [JsonProperty(Order = 20, PropertyName = "member")]
        public Id[] Members { get; set; }

        public HydraStringIdCollection(IEnumerable<string> ids)
        {
            Members = ids.Select(id => new Id(id)).ToArray();
        }
    }
}
