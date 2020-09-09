using System.Collections.Generic;
using IIIF.Presentation.Content;

namespace IIIF.Presentation
{
    public class Agent : ResourceBase
    {
        public override string Type => nameof(Agent);
        public List<Image>? Logo { get; set; }
    }
}
