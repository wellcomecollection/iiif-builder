using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utils;
using Utils.Web;

namespace OAuth2
{
    public class OAuth2ApiConsumer
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<OAuth2ApiConsumer> logger;

        // A collection of tokens by scope
        private static readonly Dictionary<string, OAuth2Token> Tokens = new Dictionary<string, OAuth2Token>();

        public OAuth2ApiConsumer(HttpClient httpClient, ILogger<OAuth2ApiConsumer> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public async Task<OAuth2Token> GetToken(ClientCredentials clientCredentials, bool credentialsInContent = false,
            bool forceNewToken = false)
        {
            try
            {
                if (!forceNewToken && HaveValidToken(clientCredentials.Scope, out var value)) return value;

                // https://tools.ietf.org/html/rfc6749#section-2.3
                var data = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials"
                };

                if (clientCredentials.Scope.HasText())
                {
                    data["scope"] = clientCredentials.Scope;
                }

                var request = new HttpRequestMessage(HttpMethod.Post, clientCredentials.TokenEndPoint);
                if (credentialsInContent)
                {
                    data["client_id"] = clientCredentials.ClientId;
                    data["client_secret"] = clientCredentials.ClientSecret;
                }
                else
                {
                    request.Headers.AddBasicAuth(clientCredentials.ClientId, clientCredentials.ClientSecret);
                }

                request.Content = new FormUrlEncodedContent(data);
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var token = await response.Content.ReadAsAsync<OAuth2Token>();
                Tokens[clientCredentials.Scope] = token;
                return token;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error getting access_token for {scope}", clientCredentials.Scope);
                throw;
            }
        }

        public async Task<JToken> GetOAuthedJToken(
            string url, 
            ClientCredentials clientCredentials, 
            bool throwOnFailure = true)
        {
            try
            {
                var response = await MakeRequest(() => new HttpRequestMessage(HttpMethod.Get, url), clientCredentials);
                if (throwOnFailure)
                {
                    response.EnsureSuccessStatusCode();
                }
                var jsonStr = await response.Content.ReadAsStringAsync();
                return JToken.Parse(jsonStr);
            }
            catch (HttpRequestException webex)
            {
                logger.LogError(webex, "Error calling {url}", url);
                throw;
            }
        }

        public async Task<PostBodyResponse> PostBody(
            string requestUrl,
            string body,
            ClientCredentials clientCredentials
        )
        {
            var response = await MakeRequest(() => new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }, 
                clientCredentials);
            var result = new PostBodyResponse
            {
                HttpStatusCode = response.StatusCode
            };
            try
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                if (jsonStr.HasText())
                {
                    result.ResponseObject = JObject.Parse(jsonStr);
                }
            }
            catch(Exception ex)
            {
                result.TransportError = ex.Message;
            }
            return result;
        }

        // NOTE: This needs Func<HttpRequestMessage> rather than just HttpRequestMessage to avoid getting
        // "Cannot send the same request message multiple times" error
        private async Task<HttpResponseMessage> MakeRequest(
            Func<HttpRequestMessage> requestMaker,
            ClientCredentials clientCredentials,
            bool isRetry = false)
        {
            var token = await GetToken(clientCredentials, forceNewToken: isRetry);
            var request = requestMaker();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var response = await httpClient.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Forbidden && !isRetry)
            {
                logger.LogWarning("Got 403 with token for scope {scope} but has ttl {ttl}s. Forcing new token and retrying..",
                    clientCredentials.Scope, token.GetTimeToLive().TotalSeconds);
                return await MakeRequest(requestMaker, clientCredentials, true);
            }

            return response;
        }
        
        private static bool HaveValidToken(string scope, out OAuth2Token value)
        {
            var haveToken = Tokens.TryGetValue(scope, out var currentToken);
            if (haveToken && !(currentToken.GetTimeToLive().TotalSeconds < 60))
            {
                value = currentToken;
                return true;
            }

            value = null;
            return false;
        }
    }
}
