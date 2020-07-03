using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Utils;
using Utils.Caching;
using Wellcome.Dds.AssetDomain;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class ArchiveStorageServiceWorkStorageFactory : IWorkStorageFactory
    {
        private IAmazonS3 storageServiceS3;
        private ILogger<ArchiveStorageServiceWorkStorageFactory> logger;
        private StorageOptions storageOptions;
        internal readonly ISimpleCache Cache;
        private readonly IBinaryObjectCache<WellcomeBagAwareArchiveStorageMap> storageMapCache;
        private WellcomeApiToken Token { get; set; }

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
            IAmazonS3 storageServiceS3)
        {
            this.logger = logger;
            this.storageOptions = storageOptions.Value;
            this.storageMapCache = storageMapCache;
            Cache = cache;
            this.storageServiceS3 = storageServiceS3;
            // storageMapCache = new BinaryFileCacheManager<WellcomeBagAwareArchiveStorageMap>(cacheFolder, "storagemap_", httpRuntimeCacheSeconds);
        }

        private bool NeedsRebuilding (WellcomeBagAwareArchiveStorageMap map)
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

        public IWorkStore GetWorkStore(string identifier)
        {
            Func<WellcomeBagAwareArchiveStorageMap> getFromSource = () => BuildStorageMap(identifier);
            logger.LogInformation("Getting IWorkStore for " + identifier);
            WellcomeBagAwareArchiveStorageMap storageMap = storageMapCache.GetCachedObject(identifier, getFromSource, NeedsRebuilding);
            return new ArchiveStorageServiceWorkStore(identifier, storageMap, this, storageServiceS3);
        }

        private WellcomeBagAwareArchiveStorageMap BuildStorageMap(string identifier)
        {
            logger.LogInformation("Requires new build of storage map for " + identifier);
            var storageManifest = LoadStorageManifest(identifier);
            return BuildStorageMapFromManifest(storageManifest, identifier);
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

        private JObject LoadStorageManifest(string identifier)
        {
            logger.LogInformation("Getting storage manifest for " + identifier);
            JObject storageManifest = null;
            Exception apiException = null;
            int tries = 1;
            while (tries <= 3 && storageManifest == null)
            {
                try
                {
                    storageManifest = GetStorageManifest(identifier);
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
                    logger.LogError("Unable to load storage manifest for " + identifier, apiException);
                    throw new ApplicationException("Unable to load storage manifest for "
                                                   + identifier + " - " + apiException.Message, apiException);
                }
                logger.LogError("Unable to load storage manifest for " + identifier);
                throw new ApplicationException("Unable to load storage manifest for " + identifier);
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

        public JObject GetStorageManifest(string identifier)
        {
            var storageManifestUrl = String.Format(storageOptions.StorageApiTemplate, identifier);
            // temporary workaround to Cloudfront 404 caching
            storageManifestUrl += "?ts=" + DateTime.Now.Ticks;
            return (JObject)GetOAuthedJToken(storageManifestUrl);
        }

        private JToken GetOAuthedJToken(string url, string scope = null)
        {
            Debug.WriteLine("######### " + url);
            var accessToken = GetToken(scope).AccessToken;
            using (WebClient client = WebClientProvider.GetWebClient
                (withProxy:true, 
                requestCacheLevel:RequestCacheLevel.NoCacheNoStore))
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.Authorization] = "Bearer " + accessToken;
                try
                {
                    var jsonStr = client.DownloadString(url);
                    WebClientProvider.DebugHeaders(client.Headers);
                    Debug.WriteLine("-");
                    WebClientProvider.DebugHeaders(client.ResponseHeaders);
                    return JToken.Parse(jsonStr);

                }
                catch (WebException webex)
                {
                    Debug.Write(webex.Message);
                    WebClientProvider.DebugHeaders(client.Headers);
                    Debug.WriteLine("-");
                    if (webex.Response != null)
                    {
                        WebClientProvider.DebugHeaders(webex.Response.Headers);
                        Debug.WriteLine("IsFromCache: " + webex.Response.IsFromCache);
                    }
                    throw;
                }
            }
        }
        
        public WellcomeApiToken GetToken(string scope = null)
        {
            if (Token == null || Token.GetTimeToLive().TotalSeconds < 60)
            {
                string responseString;
                using (var wb = WebClientProvider.GetWebClient())
                {
                    var data = new NameValueCollection();
                    data["grant_type"] = "client_credentials";
                    data["client_id"] = storageOptions.ClientId;
                    data["client_secret"] = storageOptions.ClientSecret;
                    if (scope == null) scope = storageOptions.Scope;
                    data["scope"] = scope;
                    var response = wb.UploadValues(storageOptions.TokenEndPoint, "POST", data);
                    responseString = Encoding.UTF8.GetString(response);
                }
               
                var jObj = JObject.Parse(responseString);
                Token = new WellcomeApiToken
                {
                    Acquired = DateTime.Now,
                    AccessToken = jObj["access_token"].Value<string>(),
                    TokenType   = jObj["token_type"].Value<string>(),
                    ExpiresIn   = jObj["expires_in"].Value<int>()
                };
            }
            return Token;
        }

        public JObject GetIngest(string ingestId)
        {
            var url = storageOptions.StorageApiTemplateIngest + "/" + ingestId;
            return (JObject)GetOAuthedJToken(url, storageOptions.ScopeIngest);
        }

        // This API has been retired
        //public JToken GetIngestsForIdentifier(string identifier)
        //{
        //    var url = string.Format(storageOptions. IngestsForIdTemplate, identifier);
        //    return GetOAuthedJToken(url, IngestScope);
        //}

        public JObject PostIngest(string body)
        {
            var accessToken = GetToken(storageOptions.ScopeIngest).AccessToken;
            using (WebClient client = WebClientProvider.GetWebClient())
            {
                client.Headers[HttpRequestHeader.Authorization] = "Bearer " + accessToken;
                var jsonStr = client.UploadString(storageOptions.StorageApiTemplateIngest, body);
                return JObject.Parse(jsonStr);
            }
        }
    }

    

    /// <summary>
    /// This is a OAuth2 token acquired via the Client Credentials grant type
    /// </summary>
    public class WellcomeApiToken
    {
        public string TokenType { get; set; }
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime Acquired { get; set; }

        public TimeSpan GetTimeToLive()
        {
            return (Acquired + new TimeSpan(0, 0, ExpiresIn)) - DateTime.Now;
        }  
    }
}
