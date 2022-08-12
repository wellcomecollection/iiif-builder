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
        string Identifier { get; }
        Task<XmlSource> LoadXmlForPath(string relativePath);
        Task<XmlSource> LoadXmlForPath(string relativePath, bool useCache);
        Task<XmlSource> LoadXmlForIdentifier(string identifier);
        IArchiveStorageStoredFileInfo GetFileInfoForPath(string relativePath);
        Task<Stream> GetStreamForPathAsync(string relativePath);

        /// <summary>
        /// Factory method for asset metadata, which depends on the XML structure
        /// TODO: this doesn't belong in this interface in the new IIIFBuilder - 
        /// It ties the storage implementation to the metadata implementation,
        /// expecting that a different storage layer will use different metadata (which WAS the case)
        /// Now, we can assume that it's all Premis whether the storage is FileSystem or WellcomeStorage.
        /// </summary>
        /// <param name="metsRoot"></param>
        /// <param name="admId"></param>
        /// <returns></returns>
        IAssetMetadata MakeAssetMetadata(XElement metsRoot, string admId);
        
        
        /// <summary>
        /// Returns the appropriate starting document for this work.
        /// Currently will be
        /// {bnumber}.xml for digitised (the Goobi METS or anchor file)
        /// or
        /// METS.{guid}.xml for born-digital (the Archivematica METS file)
        /// </summary>
        /// <returns>A relative file name of the METS file to start with</returns>
        string GetRootDocument();
    }
}
