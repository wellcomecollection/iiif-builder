using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IIIF.Presentation.V2
{
    /// <summary>
    /// The manifest response contains sufficient information for the client to initialize itself and begin to display
    /// something quickly to the user.
    /// </summary>
    /// <remarks>See https://iiif.io/api/presentation/2.1/#manifest</remarks>
    public class Manifest : IIIFPresentationBase
    {
        public override string Type => "sc:Manifest";
        
        [JsonProperty(Order = 31, PropertyName = "viewingDirection")]
        public string? ViewingDirection { get; set; }

        [JsonProperty(Order = 40, PropertyName = "sequences")]
        public List<Sequence> Sequences { get; set; }
        
        [JsonProperty(Order = 50, PropertyName = "structures")]
        public List<Range>? Structures { get; set; }
    }

    public class Range : IIIFPresentationBase
    {
        public override string Type => "sc:Range";
        
        [JsonProperty(Order = 31, PropertyName = "viewingDirection")]
        public string? ViewingDirection { get; set; }

        [JsonProperty(Order = 31, PropertyName = "startCanvas")]
        public Uri? StartCanvas { get; set; }
        
        // URIs of ranges
        [JsonProperty(Order = 41, PropertyName = "ranges")]
        public List<string>? Ranges { get; set; }
        
        // URIs of canvases
        [JsonProperty(Order = 42, PropertyName = "canvases")]
        public List<string>? Canvases { get; set; }
    }
    
    public class Sequence : IIIFPresentationBase
    {
        public override string Type => "sc:Sequence";
        
        [JsonProperty(Order = 31, PropertyName = "startCanvas")]
        public string StartCanvas { get; set; }

        [JsonProperty(Order = 31, PropertyName = "viewingDirection")]
        public string ViewingDirection { get; set; }

        [JsonProperty(Order = 50, PropertyName = "canvases")]
        public List<Canvas> Canvases { get; set; }
    }

    public class Canvas : IIIFPresentationBase
    {
        public override string Type => "sc:Canvas";
        
        [JsonProperty(Order = 35, PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(Order = 36, PropertyName = "width")]
        public int Width { get; set; }

        // Link to Image resources
        [JsonProperty(Order = 60, PropertyName = "images")]
        public List<ImageAnnotation> Images { get; set; }
    }
    
    public class ImageAnnotation : LegacyResourceBase
    {
        public override string Type => "oa:Annotation";

        [JsonProperty(Order = 4, PropertyName = "motivation")]
        public string Motivation => "sc:painting";

        [JsonProperty(Order = 10, PropertyName = "resource")]
        public ImageResource Resource { get; set; }

        [JsonProperty(Order = 36, PropertyName = "on")]
        public string On { get; set; }
    }
    
    public class ImageResource : Resource
    {
        public override string Type => "dctypes:Image";
        
        [JsonProperty(Order = 35, PropertyName = "height")]
        public int? Height { get; set; }

        [JsonProperty(Order = 36, PropertyName = "width")]
        public int? Width { get; set; }
    }
}