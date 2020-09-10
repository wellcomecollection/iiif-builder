using System.Collections.Generic;
using Newtonsoft.Json;

namespace IIIF.ImageApi.Service
{
    public class ImageService2 : IService
    {
        public const string Image2Context = "http://iiif.io/api/image/2/context.json";
        public const string Level0Profile = "http://iiif.io/api/image/2/level0.json";
        public const string Level1Profile = "http://iiif.io/api/image/2/level1.json";
        public const string Level2Profile = "http://iiif.io/api/image/2/level2.json";
        
        [JsonProperty(PropertyName = "@context", Order = 1)]
        public string Context { get; set; } // must be the above, or null
        
        [JsonProperty(PropertyName = "@id", Order = 2)]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "@type", Order = 3)]
        public string Type => nameof(ImageService2);
        
        [JsonProperty(Order = 4)]
        public string Profile { get; set; } // must be one of the above, or null
        // TODO: full offering
        // public Image2Profile Profile { get; set; } 
        
        [JsonProperty(Order = 11)]
        public int Width { get; set; }
        
        [JsonProperty(Order = 12)]
        public int Height { get; set; }

        [JsonProperty(Order = 13)] 
        public List<Size> Sizes { get; set; }

        [JsonProperty(Order = 14)]
        public List<Tile> Tiles { get; set; }
        
        // TODO - Attribution, logo; not needed right now
        
        [JsonProperty(Order = 20)]
        public string[] License { get; set; }
        
        // TODO - Auth Services
    }
}