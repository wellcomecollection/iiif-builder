using System.Collections.Generic;

namespace IIIF.Presentation.Annotation
{
    public class PaintingChoice : IPaintable
    {
        public string Type => "Choice";
        public List<IPaintable>? Items { get; set; }
        
        public List<IService>? Service { get; set; }
    }
}