using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Utils.Aws.S3;
using Utils.Caching;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService
{
    /// <summary>
    /// Implementation of <see cref="IWorkStorageFactory"/> for works from Wellcome storage service.
    /// </summary>
    public class ArchiveStorageServiceWorkStorageFactory : IWorkStorageFactory
    {
        private readonly StorageServiceClient storageServiceClient;
        private readonly IAmazonS3 storageServiceS3;
        private readonly ILogger<ArchiveStorageServiceWorkStorageFactory> logger;
        private readonly StorageOptions storageOptions;
        private readonly IBinaryObjectCache<WellcomeBagAwareArchiveStorageMap> storageMapCache;
        private readonly Dictionary<string, XElement> xmlElementCache = new();

        public ArchiveStorageServiceWorkStorageFactory(
            ILogger<ArchiveStorageServiceWorkStorageFactory> logger,
            IOptions<StorageOptions> storageOptions,
            IBinaryObjectCache<WellcomeBagAwareArchiveStorageMap> storageMapCache,
            INamedAmazonS3ClientFactory storageServiceS3,
            StorageServiceClient storageServiceClient)
        {
            this.logger = logger;
            this.storageOptions = storageOptions.Value;
            this.storageMapCache = storageMapCache;
            this.storageServiceClient = storageServiceClient;
            this.storageServiceS3 = storageServiceS3.Get(NamedClient.Storage);
        }

        public async Task<IWorkStore> GetWorkStore(DdsIdentity ddsId)
        {
            Task<WellcomeBagAwareArchiveStorageMap?> GetFromSource() => BuildStorageMap(ddsId.StorageSpace!, ddsId.PackageIdentifier);

            logger.LogInformation("JQ {identifier} - Getting IWorkStore for ", ddsId);
            
            WellcomeBagAwareArchiveStorageMap? storageMap =
                await storageMapCache.GetCachedObject(ddsId.PackageIdentifier, GetFromSource, NeedsRebuilding);

            if (storageMap == null)
            {
                throw new InvalidOperationException($"Could not retrieve storage map for {ddsId}");
            }
            return new ArchiveStorageServiceWorkStore(
                ddsId.StorageSpace!, ddsId,
                storageMap, storageServiceClient, xmlElementCache, storageServiceS3);
        }

        private async Task<WellcomeBagAwareArchiveStorageMap?> BuildStorageMap(string storageSpace, string packageIdentifier)
        {
            logger.LogInformation("JQ {packageIdentifier} - Requires new build of storage map ", packageIdentifier);
            var storageManifest = await storageServiceClient.LoadStorageManifest(storageSpace, packageIdentifier);
            var wellcomeBagAwareArchiveStorageMap = WellcomeBagAwareArchiveStorageMap.FromJObject(storageManifest, packageIdentifier);
            if (wellcomeBagAwareArchiveStorageMap.VersionSets.IsNullOrEmpty())
            {
                throw new InvalidOperationException($"Unable to generate VersionSet for StorageMap. Id: {packageIdentifier}");
            }
            return wellcomeBagAwareArchiveStorageMap;
        }
        
        private bool NeedsRebuilding(WellcomeBagAwareArchiveStorageMap? map)
        {
            if (map == null)
            {
                return true;
            }
            if (map.VersionSets.IsNullOrEmpty())
            {
                logger.LogWarning("Cached StorageMap found with null or empty VersionSet. {Identifier}",
                    map.Identifier);
                return true;
            }
            
            if (storageOptions.PreferCachedStorageMap)
            {
                logger.LogInformation("JQ {identifier} - NeedsRebuilding=false because PreferCachedStorageMap", map.Identifier);
                return false;
            }

            if (storageOptions.MaxAgeStorageMap < 0)
            {
                logger.LogInformation("JQ {identifier} - NeedsRebuilding=false because MaxAgeStorageMap < 0", map.Identifier);
                return false;
            }

            if (map.Identifier == KnownIdentifiers.ChemistAndDruggist)
            {
                // Never rebuild Chemist and Druggist's storage map on demand
                return false;
            }

            var age = DateTime.UtcNow - map.Built;
            bool rebuild = age.TotalSeconds > storageOptions.MaxAgeStorageMap;
            logger.LogInformation("JQ {identifier} - NeedsRebuilding = {rebuild} because age = {age}", 
                map.Identifier, rebuild, age.TotalSeconds);
            return rebuild;
        }
    }
}
