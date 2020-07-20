using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils;
using Utils.Aws.S3;
using Utils.Caching;
using Wellcome.Dds.AssetDomain;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class ArchiveStorageServiceWorkStorageFactory : IWorkStorageFactory
    {
        internal readonly ISimpleCache Cache;
        
        private readonly IAmazonS3 storageServiceS3;
        private readonly ILogger<ArchiveStorageServiceWorkStorageFactory> logger;
        private readonly StorageOptions storageOptions;
        private readonly HttpClient httpClient;
        private readonly IBinaryObjectCache<WellcomeBagAwareArchiveStorageMap> storageMapCache;
        
        // A collection of tokens by scope
        private static readonly Dictionary<string, WellcomeApiToken> Tokens = new Dictionary<string, WellcomeApiToken>();

        //private readonly BinaryFileCacheManager<WellcomeBagAwareArchiveStorageMap> storageMapCache;
        // if using with a shared cache. But you should really only use this with a request.items cache.
        internal readonly int CacheTimeSeconds = 60;

        //// This is the length of the substring "data/"
        private const int DataPathElementOffset = 5;

        public ArchiveStorageServiceWorkStorageFactory(
            ILogger<ArchiveStorageServiceWorkStorageFactory> logger,
            IOptions<StorageOptions> storageOptions,
            IBinaryObjectCache<WellcomeBagAwareArchiveStorageMap> storageMapCache,
            ISimpleCache cache,
            INamedAmazonS3ClientFactory storageServiceS3,
            HttpClient httpClient)
        {
            this.logger = logger;
            this.storageOptions = storageOptions.Value;
            this.storageMapCache = storageMapCache;
            Cache = cache;
            this.storageServiceS3 = storageServiceS3.Get(NamedClient.Storage);
            this.httpClient = httpClient;
        }

        private bool NeedsRebuilding(WellcomeBagAwareArchiveStorageMap map)
        {
            if (storageOptions.PreferCachedStorageMap)
            {
                return false;
            }

            if (storageOptions.MaxAgeStorageMap < 0)
            {
                return false;
            }

            var age = DateTime.Now - map.Built;
            return age.TotalSeconds > storageOptions.MaxAgeStorageMap;
        }

        public async Task<IWorkStore> GetWorkStore(string identifier)
        {
            Func<Task<WellcomeBagAwareArchiveStorageMap>> getFromSource = () => BuildStorageMap(identifier);
            logger.LogInformation("Getting IWorkStore for {identifier}", identifier);
            WellcomeBagAwareArchiveStorageMap storageMap = await storageMapCache.GetCachedObject(identifier, getFromSource, NeedsRebuilding);
            return new ArchiveStorageServiceWorkStore(identifier, storageMap, this, storageServiceS3);
        }

        private async Task<WellcomeBagAwareArchiveStorageMap> BuildStorageMap(string identifier)
        {
            logger.LogInformation("Requires new build of storage map for {identifier}", identifier);
            var storageManifest = LoadStorageManifest(identifier);
            return BuildStorageMapFromManifest(await storageManifest, identifier);
        }

        private WellcomeBagAwareArchiveStorageMap BuildStorageMapFromManifest(JObject storageManifest, string identifier)
        {
            var accessLocation = storageManifest["location"];
            var bucketName = accessLocation["bucket"].Value<string>();
            var accessLocationPath = accessLocation["path"].Value<string>();
            var archiveStorageMap = new WellcomeBagAwareArchiveStorageMap
            {
                BucketName = bucketName,
                StorageManifestCreated = storageManifest["createdDate"].Value<DateTime>()
            };
            var pathSep = new char[] { '/' };

            var versionToFiles = new Dictionary<string, HashSet<string>>();
            foreach (var file in storageManifest["manifest"]["files"])
            {
                // strip "data/"
                // This makes an assumption that the file layout follows an expected structure
                // That's a valid assumption for the DDS to make, but not any other application using the storage
                var relativePath = file["name"].Value<string>().Substring(DataPathElementOffset);
                var version = file["path"].Value<string>().Split(pathSep).First();
                // we no longer read this; we know how it is made.
                // var awsKey = PathStringUtils.Combine(accessLocationPath, file["path"].Value<string>());
                var minRelativePath = relativePath.Replace(identifier, "#");
                if (!versionToFiles.ContainsKey(version))
                {
                    versionToFiles[version] = new HashSet<string>();
                }
                versionToFiles[version].Add(minRelativePath);
            }
            // now order the dict by largest member
            archiveStorageMap.VersionSets = versionToFiles.OrderBy(kv => kv.Value.Count).ToList();
            archiveStorageMap.Built = DateTime.Now;
            return archiveStorageMap;
        }

        private async Task<JObject> LoadStorageManifest(string identifier)
        {
            logger.LogInformation("Getting storage manifest for {identifier}", identifier);
            JObject storageManifest = null;
            Exception apiException = null;
            int tries = 1;
            while (tries <= 3 && storageManifest == null)
            {
                try
                {
                    storageManifest = await GetStorageManifest(identifier);
                }
                catch (Exception ex)
                {
                    if (!IsProtocolError(ex))
                    {
                        logger.LogError("Could not get storage manifest", ex);
                        throw;
                    }
                    logger.LogWarning("Protocol exception fetching storage manifest, will retry");
                    apiException = ex;
                    Thread.Sleep(tries*tries*1000);
                }
                tries++;
            }
            if (storageManifest == null)
            {
                if (apiException != null)
                {
                    logger.LogError(apiException, "Unable to load storage manifest for {identifier}", identifier);
                    throw new ApplicationException(
                        $"Unable to load storage manifest for {identifier} - {apiException.Message}", apiException);
                }
                logger.LogError("Unable to load storage manifest for {identifier}", identifier);
                throw new ApplicationException($"Unable to load storage manifest for {identifier}");
            }
            return storageManifest;
        }

        public bool IsProtocolError(Exception exception)
        {
            var webEx = exception as WebException;
            if (webEx != null)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError)
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<JObject> GetStorageManifest(string identifier)
        {
            var storageManifestUrl = string.Format(storageOptions.StorageApiTemplate, identifier);
            // temporary workaround to Cloudfront 404 caching
            storageManifestUrl += "?ts=" + DateTime.Now.Ticks;

            var oAuthedJToken = await GetOAuthedJToken(storageManifestUrl);
            return (JObject)oAuthedJToken;
        }

        private async Task<JToken> GetOAuthedJToken(string url, string scope = null)
        {
            Debug.WriteLine("######### " + url);
            var accessToken = (await GetToken(scope)).AccessToken;

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

        public async Task<WellcomeApiToken> GetToken(string scope = null)
        {
            var targetScope = scope ?? storageOptions.Scope;
            var haveToken = Tokens.TryGetValue(targetScope, out var currentToken);
            
            if (haveToken && !(currentToken.GetTimeToLive().TotalSeconds < 60)) return currentToken;
            
            var data = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = storageOptions.ClientId,
                ["client_secret"] = storageOptions.ClientSecret,
                ["scope"] = targetScope,
            };

            var response = await httpClient.PostAsync(storageOptions.TokenEndPoint, new FormUrlEncodedContent(data));

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadAsAsync<WellcomeApiToken>();
            Tokens[targetScope] = token;
            return token;
        }

        public async Task<JObject> GetIngest(string ingestId)
        {
            var url = storageOptions.StorageApiTemplateIngest + "/" + ingestId;
            var authedJToken = await GetOAuthedJToken(url, storageOptions.ScopeIngest);
            return (JObject)authedJToken;
        }

        public async Task<JObject> PostIngest(string body)
        {
            var ingestToken = await GetToken(storageOptions.ScopeIngest);

            var request = new HttpRequestMessage(HttpMethod.Post, storageOptions.StorageApiTemplateIngest);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ingestToken.AccessToken);

            var response = await httpClient.SendAsync(request);
            var jsonStr = await response.Content.ReadAsStringAsync();
            return JObject.Parse(jsonStr);
        }
    }

    /// <summary>
    /// This is a OAuth2 token acquired via the Client Credentials grant type
    /// </summary>
    public class WellcomeApiToken
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; }
        
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        
        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
        
        public DateTime Acquired { get; set; }

        public WellcomeApiToken()
        {
            Acquired = DateTime.Now;
        }

        public TimeSpan GetTimeToLive()
        {
            return (Acquired + new TimeSpan(0, 0, ExpiresIn)) - DateTime.Now;
        }  
    }
}
