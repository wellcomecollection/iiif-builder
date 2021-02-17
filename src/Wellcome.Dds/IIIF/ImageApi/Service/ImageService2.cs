using System.Collections.Generic;
using IIIF.Presentation.V2;
using Newtonsoft.Json;

namespace IIIF.ImageApi.Service
{
    public class ImageService2 : ResourceBase, IService
    {
        public const string Image2Context = "http://iiif.io/api/image/2/context.json";
        public const string Level0Profile = "http://iiif.io/api/image/2/level0.json";
        public const string Level1Profile = "http://iiif.io/api/image/2/level1.json";
        public const string Level2Profile = "http://iiif.io/api/image/2/level2.json";

        private string? type;
        private bool typeHasBeenSet;
        [JsonProperty(PropertyName = "@type", Order = 3)]
        public override string? Type
        {
            get => typeHasBeenSet ? type : nameof(ImageService2);
            set
            {
                type = value;
                typeHasBeenSet = true;
            }
        }

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
        [JsonProperty(Order = 28)]
        public List<IService>? Service { get; set; }
    }
}