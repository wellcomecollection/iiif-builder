using System.Collections.Generic;
using IIIF.Presentation.V2;
using Newtonsoft.Json;

namespace Wellcome.Dds.Repositories.Presentation.V2
{
    public class MediaManifest : Manifest
    {
        [JsonProperty(Order = 40, PropertyName = "mediaSequences")]
        public List<MediaSequence> MediaSequences { get; set; } = new();
    }
}