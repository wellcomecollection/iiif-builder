using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Identifiers;

namespace Wellcome.Dds.Repositories.Identifiers
{
    /// <summary>
    /// Probes the storage service through the existing <see cref="StorageServiceClient"/>, using the
    /// single-shot GetStorageManifest rather than LoadStorageManifest, whose retries would turn every
    /// miss into a 14 second wait.
    /// </summary>
    public class StorageServiceProbe : IStorageServiceProbe
    {
        private readonly StorageServiceClient storageServiceClient;
        private readonly StorageOptions storageOptions;
        private readonly ILogger<StorageServiceProbe> logger;

        public StorageServiceProbe(
            StorageServiceClient storageServiceClient,
            IOptions<StorageOptions> storageOptions,
            ILogger<StorageServiceProbe> logger)
        {
            this.storageServiceClient = storageServiceClient;
            this.storageOptions = storageOptions.Value;
            this.logger = logger;
        }

        public string StorageApi => storageOptions.StorageApiTemplate ?? "(no StorageApiTemplate configured)";

        public async Task<StorageProbeResult> Probe(string storageSpace, string identifier)
        {
            var result = new StorageProbeResult(storageSpace, identifier);
            try
            {
                var manifest = await storageServiceClient.GetStorageManifest(storageSpace, identifier);
                result.Found = true;
                result.Version = manifest.Value<string>("version");
                result.Created = manifest.Value<DateTime?>("createdDate")?.ToUniversalTime();
                var location = manifest.SelectToken("location");
                result.Bucket = location?.Value<string>("bucket");
                result.Path = location?.Value<string>("path");
            }
            catch (HttpRequestException ex)
            {
                result.StatusCode = (int?) ex.StatusCode;
                if (ex.StatusCode != HttpStatusCode.NotFound)
                {
                    result.Error = ex.Message;
                }
                logger.LogDebug("Storage probe {space}/{identifier}: {status}", storageSpace, identifier, ex.StatusCode);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                logger.LogWarning(ex, "Storage probe {space}/{identifier} failed", storageSpace, identifier);
            }
            return result;
        }
    }
}
