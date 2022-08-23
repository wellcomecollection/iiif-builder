using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;

namespace Wellcome.Dds.AssetDomainRepositories.Storage.FileSystem
{
    public class FileSystemWorkStore : IWorkStore
    {
        private string rootDirectory;
        
        public FileSystemWorkStore(string rootDirectory, string identifier)
        {
            Identifier = identifier;
            this.rootDirectory = rootDirectory;
        }

        public string Identifier { get; set; }
        private string FileUri(string relativePath)
        {
            return Path.Combine(rootDirectory, relativePath);
        }

        public async Task<XmlSource> LoadXmlForPath(string relativePath)
        {
            var path = FileUri(relativePath);
            XElement metsXml = await LoadReadOnly(path);
            return new XmlSource
            {
                XElement = metsXml,
                RelativeXmlFilePath = relativePath
            };
        }
        
        private async Task<XElement> LoadReadOnly(string filePath)
        {
            XElement xel = null;
            await using Stream s = File.OpenRead(filePath);
            xel = await XElement.LoadAsync(s, LoadOptions.None, CancellationToken.None);
            return xel;
        }

        public async Task<XmlSource> LoadXmlForPath(string relativePath, bool useCache)
        {
            // We'll never use a cache implementation here
            return await LoadXmlForPath(relativePath);
        }

        public async Task<XmlSource> LoadXmlForIdentifier(string identifier)
        {
            string relativePath = $"{identifier}.xml";
            return await LoadXmlForPath(relativePath);
        }

        public Task<XmlSource> LoadRootDocumentXml()
        {
            // Only for born digital
            throw new System.NotImplementedException();
        }

        public IArchiveStorageStoredFileInfo GetFileInfoForPath(string relativePath)
        {
            var fullPath = FileUri(relativePath);
            var fi = new FileInfo(fullPath);
            return new ArchiveStorageStoredFileInfo(
                fi.LastWriteTime,
                fullPath,
                relativePath);
        }

        public Task<Stream> GetStreamForPathAsync(string relativePath)
        {
            var fs = new FileStream(FileUri(relativePath), FileMode.Open);
            return Task.FromResult<Stream>(fs);
        }
        
        public IAssetMetadata MakeAssetMetadata(XElement metsRoot, string admId)
        {
            // TODO - when we refactor the interface, move this out - it's not tied to the STORAGE impl any more.
            return new PremisMetadata(metsRoot, admId);
        }

        public string GetRootDocument()
        {
            throw new System.NotImplementedException();
        }
    }
}