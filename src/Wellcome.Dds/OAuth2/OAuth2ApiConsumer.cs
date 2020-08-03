using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Utils;

namespace OAuth2
{
    public class OAuth2ApiConsumer
    {
        private readonly HttpClient httpClient;

        // A collection of tokens by scope
        private static readonly Dictionary<string, OAuth2Token> Tokens = new Dictionary<string, OAuth2Token>();

        public OAuth2ApiConsumer(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<OAuth2Token> GetToken(ClientCredentials clientCredentials)
        {
            var haveToken = Tokens.TryGetValue(clientCredentials.Scope, out var currentToken);
            if (haveToken && !(currentToken.GetTimeToLive().TotalSeconds < 60)) return currentToken;

            // Can we always use this approach?
            // Or do we need to use the clentId:clientSecret Sierra method?
            var data = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientCredentials.ClientId,
                ["client_secret"] = clientCredentials.ClientSecret,
                ["scope"] = clientCredentials.Scope,
            };

            var response = await httpClient.PostAsync(
                clientCredentials.TokenEndPoint,
                new FormUrlEncodedContent(data));

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadAsAsync<OAuth2Token>();
            Tokens[clientCredentials.Scope] = token;
            return token;
        }

        public async Task<JToken> GetOAuthedJToken(string url, ClientCredentials clientCredentials)
        {
            var accessToken = (await GetToken(clientCredentials)).AccessToken;

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = null;

            try
            {
                response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonStr = await response.Content.ReadAsStringAsync();

                // TODO - debugging statements need tidied
                HttpClientDebugHelpers.DebugHeaders(request.Headers);
                Debug.WriteLine("-");
                HttpClientDebugHelpers.DebugHeaders(response.Content.Headers);
                return JToken.Parse(jsonStr);
            }
            catch (HttpRequestException webex)
            {
                Debug.Write(webex.Message);
                HttpClientDebugHelpers.DebugHeaders(request.Headers);
                Debug.WriteLine("-");
                if (response != null)
                {
                    HttpClientDebugHelpers.DebugHeaders(response.Content.Headers);
                }
                throw;
            }
        }

        public async Task<JObject> PostBody(
            string requestUrl,
            string body,
            ClientCredentials clientCredentials
        )
        {
            var token = await GetToken(clientCredentials);

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var response = await httpClient.SendAsync(request);
            var jsonStr = await response.Content.ReadAsStringAsync();
            return JObject.Parse(jsonStr);
        }
    }
}
