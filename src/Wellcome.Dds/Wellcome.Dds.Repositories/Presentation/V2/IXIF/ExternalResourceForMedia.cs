using System.Collections.Generic;
using IIIF;
using IIIF.Presentation.V2;
using IIIF.Serialisation;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    public class ExternalResourceForMedia : ExternalResource
    {
        [JsonProperty(Order = 28)]
        [ObjectIfSingle]
        public List<IService>? Service { get; set; }
    }
}