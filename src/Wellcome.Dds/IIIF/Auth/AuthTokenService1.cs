using IIIF.LegacyInclusions;
using Newtonsoft.Json;

namespace IIIF.Auth
{
    public class AuthTokenService1 : LegacyResourceBase, IService
    {
        public AuthTokenService1()
        {
            Profile = "http://iiif.io/api/auth/1/token";
        }
        
        [JsonProperty(PropertyName = "@type", Order = 3)]
        public override string Type => nameof(AuthTokenService1);
        
    }
}