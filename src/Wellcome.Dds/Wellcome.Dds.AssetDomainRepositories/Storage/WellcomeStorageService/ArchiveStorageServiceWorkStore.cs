﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using Utils.Caching;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;

namespace Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService
{
    /// <summary>
    /// Workstore object from Wellcome Storage service.
    /// </summary>
    public class ArchiveStorageServiceWorkStore : IWorkStore
    {
        private readonly IAmazonS3 storageServiceS3;
        private readonly ISimpleCache cache;
        private readonly StorageServiceClient storageServiceClient;

        public WellcomeBagAwareArchiveStorageMap ArchiveStorageMap { get; }
        
        public ArchiveStorageServiceWorkStore(
            string identifier,
            WellcomeBagAwareArchiveStorageMap archiveStorageMap,
            StorageServiceClient storageServiceClient,
            IAmazonS3 storageServiceS3,
            ISimpleCache cache)
        {
            if (archiveStorageMap == null)
            {
                throw new InvalidOperationException(
                    "Cannot create a WorkStore without an ArchiveStorageMap");
            }
            Identifier = identifier;
            this.storageServiceClient = storageServiceClient;
            ArchiveStorageMap = archiveStorageMap;
            this.storageServiceS3 = storageServiceS3;
            this.cache = cache;
        }
               

        public string Identifier { get; set; }

        public bool IsKnownFile(string relativePath)
        {
            var minRelativePath = relativePath.Replace(Identifier, "#");
            foreach (var versionSet in ArchiveStorageMap.VersionSets)
            {
                // version keys are in descending order of the number of files at that version
                if (versionSet.Value.Contains(minRelativePath))
                {
                    return true;
                }
            }
            return false;
        }

        public string GetAwsKey(string relativePath)
        {
            const string awsKeyTemplate = "digitised/{0}/{1}/data/{2}";
            var minRelativePath = relativePath.Replace(Identifier, "#");
            foreach (var versionSet in ArchiveStorageMap.VersionSets)
            {
                // version keys are in descending order of the number of files at that version
                if (versionSet.Value.Contains(minRelativePath))
                {
                    return string.Format(awsKeyTemplate, Identifier, versionSet.Key, relativePath);
                }
            }
            throw new FileNotFoundException("File not present in storage map: " + relativePath, relativePath);
        }

        public string FileUri(string relativePath)
        {
            const string fullUriTemplate = "https://s3-eu-west-1.amazonaws.com/{0}/{1}";
            var foundKey = GetAwsKey(relativePath);
            return string.Format(fullUriTemplate, ArchiveStorageMap.BucketName, foundKey);
        }

        public Task<XmlSource> LoadXmlForPath(string relativePath) => LoadXmlForPath(relativePath, true);

        public async Task<XmlSource> LoadXmlForPath(string relativePath, bool useCache)
        {
            const int cacheTimeSeconds = 60;
            XElement metsXml;
            if (useCache)
            {
                metsXml = await cache.GetCached(cacheTimeSeconds, relativePath, () => LoadXElementAsync(relativePath));
            }
            else
            {
                metsXml = await LoadXElementAsync(relativePath);
            }
            return new XmlSource
            {
                XElement = metsXml,
                RelativeXmlFilePath = relativePath
            };
        }

        public async Task<XmlSource> LoadXmlForIdentifier(string identifier)
        {
            string relativePath = $"{identifier}.xml";
            return await LoadXmlForPath(relativePath);
        }

        public IArchiveStorageStoredFileInfo GetFileInfoForIdentifier(string identifier)
        {
            var relativePath = $"{identifier}.xml";
            return GetFileInfoForPath(relativePath);
        }

        public IArchiveStorageStoredFileInfo GetFileInfoForPath(string relativePath)
        {
            return new ArchiveStorageStoredFileInfo(
                ArchiveStorageMap.StorageManifestCreated, 
                FileUri(relativePath),
                relativePath);
        }

        public async Task<Stream> GetStreamForPathAsync(string relativePath)
        {
            var req = MakeGetObjectRequest(relativePath);
            using (GetObjectResponse response = await storageServiceS3.GetObjectAsync(req))
            using (Stream responseStream = response.ResponseStream)
            {
                MemoryStream stream = new MemoryStream();
                await responseStream.CopyToAsync(stream);
                stream.Position = 0;
                return stream;
            }
        }

        private GetObjectRequest MakeGetObjectRequest(string relativePath)
        {
            return new GetObjectRequest
            {
                BucketName = ArchiveStorageMap.BucketName,
                Key = GetAwsKey(relativePath)
            };
        }

        private async Task<XElement> LoadXElementAsync(string relativePath)
        {
            var req = MakeGetObjectRequest(relativePath);
            using (GetObjectResponse response = await storageServiceS3.GetObjectAsync(req))
            using (Stream responseStream = response.ResponseStream)
            {
                return XElement.Load(responseStream);
            }
        }

        public IAssetMetadata MakeAssetMetadata(XElement metsRoot, string admId)
        {
            return new PremisMetadata(metsRoot, admId);
        }

        public Task<JObject> GetStorageManifest() => storageServiceClient.GetStorageManifest(Identifier);

        public async Task WriteFileAsync(string relativePath, string destination)
        {
            var req = MakeGetObjectRequest(relativePath);
            using (GetObjectResponse response = await storageServiceS3.GetObjectAsync(req))
            {
                // TODO: What should the cancellation token be?
                await response.WriteResponseStreamToFileAsync(destination, false, CancellationToken.None);
            }
        }
    }
}