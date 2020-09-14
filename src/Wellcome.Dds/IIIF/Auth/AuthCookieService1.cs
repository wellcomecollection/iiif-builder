using System.Collections.Generic;
using IIIF.LegacyInclusions;
using Newtonsoft.Json;

namespace IIIF.Auth
{
    public class AuthCookieService1 : ServiceBase, IService
    {
        private const string LoginProfile = "http://iiif.io/api/auth/1/login";
        private const string ClickthroughProfile = "http://iiif.io/api/auth/1/clickthrough";
        private const string KioskProfile = "http://iiif.io/api/auth/1/kiosk";
        private const string ExternalProfile = "http://iiif.io/api/auth/1/external";

        public AuthCookieService1(string profile)
        {
            Profile = profile;
        }
        
        [JsonProperty(PropertyName = "@type", Order = 3)]
        public override string Type => nameof(AuthCookieService1);
        
        [JsonProperty(Order = 12, PropertyName = "description")]
        public MetaDataValue Description { get; set; }

        [JsonProperty(Order = 26, PropertyName = "service")]
        public List<IService> Service { get; set; } // object or array of objects
        
        [JsonProperty(Order = 103, PropertyName = "confirmLabel")]
        public MetaDataValue? ConfirmLabel { get; set; }

        [JsonProperty(Order = 111, PropertyName = "header")]
        public MetaDataValue? Header { get; set; }

        [JsonProperty(Order = 121, PropertyName = "failureHeader")]
        public MetaDataValue? FailureHeader { get; set; }

        [JsonProperty(Order = 122, PropertyName = "failureDescription")]
        public MetaDataValue? FailureDescription { get; set; }
        
        public static AuthCookieService1 NewLoginInstance()
        {
            return new AuthCookieService1(LoginProfile);
        }
        public static AuthCookieService1 NewClickthroughInstance()
        {
            return new AuthCookieService1(ClickthroughProfile);
        }
        public static AuthCookieService1 NewKioskInstance()
        {
            return new AuthCookieService1(KioskProfile);
        }
        public static AuthCookieService1 NewExternalInstance()
        {
            return new AuthCookieService1(ExternalProfile);
        }
    }
}