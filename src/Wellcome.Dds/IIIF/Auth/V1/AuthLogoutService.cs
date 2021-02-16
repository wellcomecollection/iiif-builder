using IIIF.Presentation.V2;
using Newtonsoft.Json;

namespace IIIF.Auth.V1
{
    public class AuthLogoutService: LegacyResourceBase, IService
    {
        public AuthLogoutService()
        {
            Profile = "http://iiif.io/api/auth/1/logout";
        }
        
        [JsonProperty(PropertyName = "@type", Order = 3)]
        public override string Type => nameof(AuthLogoutService);
    }
}