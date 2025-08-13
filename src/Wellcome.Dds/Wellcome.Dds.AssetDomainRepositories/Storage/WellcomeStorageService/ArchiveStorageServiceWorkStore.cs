using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService
{
    /// <summary>
    /// Workstore object from Wellcome Storage service.
    /// </summary>
    public class ArchiveStorageServiceWorkStore : IWorkStore
    {
        private readonly IAmazonS3 storageServiceS3;
        private readonly StorageServiceClient storageServiceClient;
        private readonly Dictionary<string, XElement> xmlElementCache; 

        public WellcomeBagAwareArchiveStorageMap ArchiveStorageMap { get; }
        
        public ArchiveStorageServiceWorkStore(
            string storageSpace,
            DdsIdentity identifier,
            WellcomeBagAwareArchiveStorageMap archiveStorageMap,
            StorageServiceClient storageServiceClient,
            Dictionary<string, XElement> elementCache,
            IAmazonS3 storageServiceS3)
        {
            if (archiveStorageMap == null)
            {
                throw new InvalidOperationException(
                    "Cannot create a WorkStore without an ArchiveStorageMap");
            }

            xmlElementCache = elementCache;
            Identifier = identifier;            
            StorageSpace = storageSpace;
            this.storageServiceClient = storageServiceClient;
            ArchiveStorageMap = archiveStorageMap;
            this.storageServiceS3 = storageServiceS3;
        }

        public DdsIdentity Identifier { get; }        
        public string StorageSpace { get; }

        public string GetAwsKey(string relativePath)
        {            
            var minRelativePath = relativePath.Replace(Identifier.Value, "#");
            foreach (var versionSet in ArchiveStorageMap.VersionSets)
            {
                // version keys are in descending order of the number of files at that version
                if (versionSet.Value.Contains(minRelativePath))
                {
                    return $"{StorageSpace}/{Identifier}/{versionSet.Key}/data/{relativePath}";
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
            if (useCache && xmlElementCache.TryGetValue(relativePath, out var metsXml))
            {
                return new XmlSource(metsXml, relativePath);
            }
            
            metsXml = await LoadXElementAsync(relativePath);

            if (useCache)
            {
                xmlElementCache[relativePath] = metsXml;
            }

            return new XmlSource(metsXml, relativePath);
        }

        public async Task<XmlSource> LoadXmlForIdentifier(string identifier)
        {
            string relativePath = $"{identifier}.xml";
            return await LoadXmlForPath(relativePath);
        }

        public async Task<XmlSource> LoadRootDocumentXml()
        {
            return await LoadXmlForPath(GetRootDocument());
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
        
        public string GetRootDocument()
        {
            // This logic works for our current two cases.
            if (ArchiveStorageMap.OtherIdentifier.HasText())
            {
                return $"METS.{ArchiveStorageMap.OtherIdentifier}.xml";
            }

            return $"{ArchiveStorageMap.Identifier}.xml";
        }

        public Task<JObject> GetStorageManifest() => storageServiceClient.GetStorageManifest(StorageSpace, Identifier.Value);
    }
}
