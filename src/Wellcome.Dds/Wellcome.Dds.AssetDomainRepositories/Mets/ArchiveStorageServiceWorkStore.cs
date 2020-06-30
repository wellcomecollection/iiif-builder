using System;
using System.IO;
using System.Xml.Linq;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class ArchiveStorageServiceWorkStore : IWorkStore
    {
        private AmazonS3Client s3Client;
        private readonly ArchiveStorageServiceWorkStorageFactory factory;

        public WellcomeBagAwareArchiveStorageMap ArchiveStorageMap { get; private set; }
        
        public ArchiveStorageServiceWorkStore(
            string identifier,
            WellcomeBagAwareArchiveStorageMap archiveStorageMap,
            ArchiveStorageServiceWorkStorageFactory factory)
        {
            if (archiveStorageMap == null)
            {
                throw new InvalidOperationException(
                    "Cannot create a WorkStore without an ArchiveStorageMap");
            }
            Identifier = identifier;
            this.factory = factory;
            ArchiveStorageMap = archiveStorageMap;
        }

        private AmazonS3Client GetS3Client()
        {
            if (s3Client == null)
            {
                var credentials = new StoredProfileAWSCredentials("storage");
                s3Client = new AmazonS3Client(credentials, RegionEndpoint.EUWest1);
            }
            return s3Client;
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

        public XmlSource LoadXmlForPath(string relativePath)
        {
            return LoadXmlForPath(relativePath, true);
        }

        public XmlSource LoadXmlForPath(string relativePath, bool useCache)
        {
            XElement metsXml;
            if (useCache)
            {
                metsXml = factory.Cache.GetCached(factory.CacheTimeSeconds, relativePath, () => LoadXElement(relativePath));
            }
            else
            {
                metsXml = LoadXElement(relativePath);
            }
            return new XmlSource
            {
                XElement = metsXml,
                RelativeXmlFilePath = relativePath
            };
        }

        public XmlSource LoadXmlForIdentifier(string identifier)
        {
            string relativePath = identifier + ".xml";
            return LoadXmlForPath(relativePath);
        }

        public IStoredFileInfo GetFileInfoForIdentifier(string identifier)
        {
            var relativePath = identifier + ".xml";
            return GetFileInfoForPath(relativePath);
        }

        public IStoredFileInfo GetFileInfoForPath(string relativePath)
        {
            return new ArchiveStorageStoredFileInfo(
                ArchiveStorageMap.StorageManifestCreated, 
                FileUri(relativePath),
                relativePath);
        }

        public Stream GetStreamForPath(string relativePath)
        {
            var req = MakeGetObjectRequest(relativePath);
            using (GetObjectResponse response = GetS3Client().GetObject(req))
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

        private XElement LoadXElement(string relativePath)
        {
            var req = MakeGetObjectRequest(relativePath);
            using (GetObjectResponse response = GetS3Client().GetObject(req))
            using (Stream responseStream = response.ResponseStream)
            {
                return XElement.Load(responseStream);
            }
        }

        public IAssetMetadata MakeAssetMetadata(XElement metsRoot, string admId)
        {
            return new PremisMetadata(metsRoot, admId);
        }

        public JObject GetStorageManifest()
        {
            return factory.GetStorageManifest(Identifier);
        }

        public void WriteFile(string relativePath, string destination)
        {
            var req = MakeGetObjectRequest(relativePath);
            using (GetObjectResponse response = GetS3Client().GetObject(req))
            {
                response.WriteResponseStreamToFile(destination);
            }
        }
    }
}
