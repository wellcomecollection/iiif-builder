﻿using System;
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

        public async Task<IWorkStore> GetWorkStore(string identifier)
        {
            Func<Task<WellcomeBagAwareArchiveStorageMap>> getFromSource = () => BuildStorageMap(identifier);
            logger.LogInformation("Getting IWorkStore for {identifier}", identifier);
            WellcomeBagAwareArchiveStorageMap storageMap =
                await storageMapCache.GetCachedObject(identifier, getFromSource, NeedsRebuilding);
            return new ArchiveStorageServiceWorkStore(identifier, storageMap, storageServiceClient, xmlElementCache,
                storageServiceS3);
        }
        
        private async Task<WellcomeBagAwareArchiveStorageMap> BuildStorageMap(string identifier)
        {
            logger.LogInformation("Requires new build of storage map for {identifier}", identifier);
            var storageManifest = await storageServiceClient.LoadStorageManifest(identifier);
            return WellcomeBagAwareArchiveStorageMap.FromJObject(storageManifest, identifier);
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

            if (map.Identifier == KnownIdentifiers.ChemistAndDruggist)
            {
                // Never rebuild Chemist and Druggist's storage map on demand
                return false;
            }
    
            var age = DateTime.UtcNow - map.Built;
            return age.TotalSeconds > storageOptions.MaxAgeStorageMap;
        }
    }
}
