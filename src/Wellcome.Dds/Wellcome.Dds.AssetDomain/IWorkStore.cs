using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain
{
    /// <summary>
    /// Interface for data related to a single bNumber.
    /// </summary>
    public interface IWorkStore
    {
        string Identifier { get; set; }
        string FileUri(string relativePath);
        Task<XmlSource> LoadXmlForPathAsync(string relativePath);
        Task<XmlSource> LoadXmlForPathAsync(string relativePath, bool useCache);
        Task<XmlSource> LoadXmlForIdentifierAsync(string identifier);
        IArchiveStorageStoredFileInfo GetFileInfoForIdentifier(string identifier);
        IArchiveStorageStoredFileInfo GetFileInfoForPath(string relativePath);
        Task<Stream> GetStreamForPathAsync(string relativePath);
        Task WriteFileAsync(string relativePath, string destination);
        bool IsKnownFile(string relativePath);

        /// <summary>
        /// Factory method for asset metadata, which depends on the XML structure
        /// </summary>
        /// <param name="metsRoot"></param>
        /// <param name="admId"></param>
        /// <returns></returns>
        IAssetMetadata MakeAssetMetadata(XElement metsRoot, string admId);
    }
}
