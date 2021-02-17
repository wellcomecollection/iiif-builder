using IIIF.Presentation.V2;
using Newtonsoft.Json;

namespace IIIF.Auth.V1
{
    public class AuthTokenService : ResourceBase, IService
    {
        public AuthTokenService()
        {
            Profile = "http://iiif.io/api/auth/1/token";
        }
        
        private string? type;
        private bool typeHasBeenSet;
        [JsonProperty(PropertyName = "@type", Order = 3)]
        public override string? Type
        {
            get => typeHasBeenSet ? type : "AuthTokenService1";
            set
            {
                type = value;
                typeHasBeenSet = true;
            }
        }
    }
}