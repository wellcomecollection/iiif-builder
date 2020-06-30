using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using Digirati.Util;
using Digirati.Util.Caching;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Wellcome.Dds.AssetDomain;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class ArchiveStorageServiceWorkStorageFactory : IWorkStorageFactory
    {
        private ILogger<ArchiveStorageServiceWorkStorageFactory> logger;

        internal readonly ISimpleCache Cache;

        private readonly BinaryFileCacheManager<WellcomeBagAwareArchiveStorageMap> storageMapCache;
        // if using with a shared cache. But you should really only use this with a request.items cache.
        internal readonly int CacheTimeSeconds = 60;
        
        private static readonly string ClientId = StringUtils.GetAppSetting("ArchiveStorage-ClientId", null);
        private static readonly string ClientSecret = StringUtils.GetAppSetting("ArchiveStorage-ClientSecret", null);
        private static readonly string StorageApiTemplate = StringUtils.GetAppSetting("ArchiveStorage-StorageApiTemplate", null);
        private static readonly string TokenEndPoint = StringUtils.GetAppSetting("ArchiveStorage-TokenEndPoint", null);
        private static readonly string Scope = StringUtils.GetAppSetting("ArchiveStorage-Scope", null);


        // This is the length of the substring "data/"
        private const int DataPathElementOffset = 5;

        private static readonly bool PreferCachedStorageMap =
            StringUtils.GetBoolFromAppSetting("ArchiveStorage-PreferCachedStorageMap", true);
        private static readonly int MaxAgeStorageMap =
            StringUtils.GetInt32FromAppSetting("ArchiveStorage-MaxAgeStorageMap", -1);

        // for migration:
        private static readonly string IngestScope = StringUtils.GetAppSetting("ArchiveStorage-ScopeIngest", null);
        private static readonly string IngestTemplate = StringUtils.GetAppSetting("ArchiveStorage-StorageApiTemplateIngest", null);

        private static readonly string IngestsForIdTemplate =
            StringUtils.GetAppSetting("ArchiveStorage-StorageApiIngestsForIdTemplate", null);
            
        private WellcomeApiToken Token { get; set; }
        
        public ArchiveStorageServiceWorkStorageFactory(
            ILogger<ArchiveStorageServiceWorkStorageFactory> logger,
            ISimpleCache cache,
            string cacheFolder,
            int httpRuntimeCacheSeconds)
        {
            this.logger = logger;
            Cache = cache;
            storageMapCache = new BinaryFileCacheManager<WellcomeBagAwareArchiveStorageMap>(cacheFolder, "storagemap_", httpRuntimeCacheSeconds);
        }

        private bool NeedsRebuilding (WellcomeBagAwareArchiveStorageMap map)
        {
            if (PreferCachedStorageMap)
            {
                return false;
            }
            if (MaxAgeStorageMap < 0)
            {
                return false;
            }
            var age = DateTime.Now - map.Built;
            return age.TotalSeconds > MaxAgeStorageMap;
        }

        public IWorkStore GetWorkStore(string identifier)
        {
            Func<WellcomeBagAwareArchiveStorageMap> getFromSource = () => BuildStorageMap(identifier);
            logger.LogInformation("Getting IWorkStore for " + identifier);
            WellcomeBagAwareArchiveStorageMap storageMap = storageMapCache.GetCachedObject(identifier, getFromSource, NeedsRebuilding);
            return new ArchiveStorageServiceWorkStore(identifier, storageMap, this);
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
                        Log.Error("Could not get storage manifest", ex);
                        throw;
                    }
                    Log.Warn("Protocol exception fetching storage manifest, will retry");
                    apiException = ex;
                    Thread.Sleep(tries*tries*1000);
                }
                tries++;
            }
            if (storageManifest == null)
            {
                if (apiException != null)
                {
                    Log.Error("Unable to load storage manifest for " + identifier, apiException);
                    throw new ApplicationException("Unable to load storage manifest for "
                                                   + identifier + " - " + apiException.Message, apiException);
                }
                Log.Error("Unable to load storage manifest for " + identifier);
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
            var storageManifestUrl = String.Format(StorageApiTemplate, identifier);
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
                    data["client_id"] = ClientId;
                    data["client_secret"] = ClientSecret;
                    if (scope == null) scope = Scope;
                    data["scope"] = scope;
                    var response = wb.UploadValues(TokenEndPoint, "POST", data);
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
            var url = IngestTemplate + "/" + ingestId;
            return (JObject)GetOAuthedJToken(url, IngestScope);
        }

        public JToken GetIngestsForIdentifier(string identifier)
        {
            var url = string.Format(IngestsForIdTemplate, identifier);
            return GetOAuthedJToken(url, IngestScope);
        }

        public JObject PostIngest(string body)
        {
            var accessToken = GetToken(IngestScope).AccessToken;
            using (WebClient client = WebClientProvider.GetWebClient())
            {
                client.Headers[HttpRequestHeader.Authorization] = "Bearer " + accessToken;
                var jsonStr = client.UploadString(IngestTemplate, body);
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
