using Newtonsoft.Json;
using System;

namespace OAuth2
{
    /// <summary>
    /// This is a OAuth2 token acquired via the Client Credentials grant type
    /// </summary>
    public class OAuth2Token
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        public DateTime Acquired { get; set; }

        public OAuth2Token()
        {
            Acquired = DateTime.Now;
        }

        public TimeSpan GetTimeToLive() => (Acquired + TimeSpan.FromSeconds(ExpiresIn)) - DateTime.Now;
    }
}
