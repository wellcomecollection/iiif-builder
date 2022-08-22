using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using OAuth2;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class StorageServiceClient
    {
        private readonly OAuth2ApiConsumer oAuth2ApiConsumer;
        private readonly ILogger<StorageServiceClient> logger;
        private readonly StorageOptions storageOptions;
        private readonly ClientCredentials defaultClientCredentials;

        // If this is needed elsewhere it could be better implemented
        private readonly Dictionary<string, JObject> requestCache = new Dictionary<string, JObject>();

        public StorageServiceClient(
            OAuth2ApiConsumer oAuth2ApiConsumer,
            IOptions<StorageOptions> storageOptions,
            ILogger<StorageServiceClient> logger)
        {
            this.oAuth2ApiConsumer = oAuth2ApiConsumer;
            this.logger = logger;
            this.storageOptions = storageOptions.Value;
            defaultClientCredentials = GetClientCredentials(storageOptions.Value);
        }
        
        public async Task<JObject> GetStorageManifest(string storageSpace, string packageIdentifier)
        {
            if (requestCache.TryGetValue(packageIdentifier, out var cachedJson))
                return cachedJson;
            
            var storageManifestUrl = string.Format(
                storageOptions.StorageApiTemplate, storageSpace, packageIdentifier);
            // temporary workaround to Cloudfront 404 caching
            storageManifestUrl += $"?ts={DateTime.Now.Ticks}";

            var storageManifestJson = await oAuth2ApiConsumer.GetOAuthedJToken(storageManifestUrl, defaultClientCredentials);
            var manifestJson = (JObject)storageManifestJson;
            requestCache[packageIdentifier] = manifestJson;
            return manifestJson;
        }
        
        public async Task<JObject> LoadStorageManifest(string storageSpace, string packageIdentifier)
        {
            logger.LogInformation("Getting storage manifest for {identifier}", packageIdentifier);
            JObject storageManifest = null;
            Exception apiException = null;
            int tries = 1;
            
            // NOTE - if we expose HttpClient we could use Polly here
            while (tries <= 3 && storageManifest == null)
            {
                try
                {
                    storageManifest = await GetStorageManifest(storageSpace, packageIdentifier);
                }
                catch (Exception ex)
                {
                    if (!IsProtocolError(ex))
                    {
                        logger.LogError(ex, "Could not get storage manifest");
                        throw;
                    }

                    logger.LogWarning("Protocol exception fetching storage manifest, will retry. Attempt: {tries}",
                        tries);
                    apiException = ex;
                    await Task.Delay(tries * tries * 1000);
                }

                tries++;
            }

            if (storageManifest == null)
            {
                if (apiException != null)
                {
                    logger.LogError(apiException, "Unable to load storage manifest for {identifier}", packageIdentifier);
                    throw new ApplicationException(
                        $"Unable to load storage manifest for {packageIdentifier} - {apiException.Message}", apiException);
                }

                logger.LogError("Unable to load storage manifest for {identifier}", packageIdentifier);
                throw new ApplicationException($"Unable to load storage manifest for {packageIdentifier}");
            }

            return storageManifest;
        }
        
        public async Task<JObject> GetIngest(string ingestId)
        {
            var ingestCredentials = new ClientCredentials
            {
                Scope = storageOptions.ScopeIngest,
                TokenEndPoint = defaultClientCredentials.TokenEndPoint,
                ClientId = defaultClientCredentials.ClientId,
                ClientSecret = defaultClientCredentials.ClientSecret
            };
            var url = $"{storageOptions.StorageApiTemplateIngest}/{ingestId}";
            var ingestJson = await oAuth2ApiConsumer.GetOAuthedJToken(url, ingestCredentials);
            return (JObject)ingestJson;
        }
        
        private static ClientCredentials GetClientCredentials(StorageOptions storageOptions) 
            =>  new ClientCredentials
            {
                TokenEndPoint = storageOptions.TokenEndPoint,
                Scope = storageOptions.Scope,
                ClientId = storageOptions.ClientId,
                ClientSecret = storageOptions.ClientSecret
            };

        private static bool IsProtocolError(Exception exception)
        {
            if (exception is WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError)
                {
                    return true;
                }
            }
            return false;
        }
    }
}