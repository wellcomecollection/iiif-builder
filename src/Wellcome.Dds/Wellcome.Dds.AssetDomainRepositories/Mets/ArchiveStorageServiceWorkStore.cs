using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class ArchiveStorageServiceWorkStore : IWorkStore
    {
        private IAmazonS3 storageServiceS3;
        private readonly ArchiveStorageServiceWorkStorageFactory factory;

        public WellcomeBagAwareArchiveStorageMap ArchiveStorageMap { get; private set; }
        
        public ArchiveStorageServiceWorkStore(
            string identifier,
            WellcomeBagAwareArchiveStorageMap archiveStorageMap,
            ArchiveStorageServiceWorkStorageFactory factory,
            IAmazonS3 storageServiceS3)
        {
            if (archiveStorageMap == null)
            {
                throw new InvalidOperationException(
                    "Cannot create a WorkStore without an ArchiveStorageMap");
            }
            Identifier = identifier;
            this.factory = factory;
            ArchiveStorageMap = archiveStorageMap;
            this.storageServiceS3 = storageServiceS3;
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

        public async System.Threading.Tasks.Task<XmlSource> LoadXmlForPathAsync(string relativePath)
        {
            return await LoadXmlForPathAsync(relativePath, true);
        }

        public async System.Threading.Tasks.Task<XmlSource> LoadXmlForPathAsync(string relativePath, bool useCache)
        {
            XElement metsXml;
            if (useCache)
            {
                metsXml = await factory.Cache.GetCached(factory.CacheTimeSeconds, relativePath, () => LoadXElementAsync(relativePath));
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

        public async System.Threading.Tasks.Task<XmlSource> LoadXmlForIdentifierAsync(string identifier)
        {
            string relativePath = identifier + ".xml";
            return await LoadXmlForPathAsync(relativePath);
        }

        public IArchiveStorageStoredFileInfo GetFileInfoForIdentifier(string identifier)
        {
            var relativePath = identifier + ".xml";
            return GetFileInfoForPath(relativePath);
        }

        public IArchiveStorageStoredFileInfo GetFileInfoForPath(string relativePath)
        {
            return new ArchiveStorageStoredFileInfo(
                ArchiveStorageMap.StorageManifestCreated, 
                FileUri(relativePath),
                relativePath);
        }

        public async System.Threading.Tasks.Task<Stream> GetStreamForPathAsync(string relativePath)
        {
            var req = MakeGetObjectRequest(relativePath);
            using (GetObjectResponse response = await storageServiceS3.GetObjectAsync(req))
            using (Stream responseStream = response.ResponseStream)
            {
                MemoryStream stream = new MemoryStream();
                responseStream.CopyTo(stream);
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

        private async System.Threading.Tasks.Task<XElement> LoadXElementAsync(string relativePath)
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

        public Task<JObject> GetStorageManifest()
        {
            return factory.GetStorageManifest(Identifier);
        }

        public async System.Threading.Tasks.Task WriteFileAsync(string relativePath, string destination)
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
