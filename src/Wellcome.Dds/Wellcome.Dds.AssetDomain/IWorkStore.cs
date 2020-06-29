using System.IO;
using System.Xml.Linq;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomain
{
    public interface IWorkStore
    {
        string Identifier { get; set; }
        string FileUri(string relativePath);
        XmlSource LoadXmlForPath(string relativePath);
        XmlSource LoadXmlForPath(string relativePath, bool useCache);
        XmlSource LoadXmlForIdentifier(string identifier);
        IStoredFileInfo GetFileInfoForIdentifier(string identifier);
        IStoredFileInfo GetFileInfoForPath(string relativePath);
        Stream GetStreamForPath(string relativePath);
        void WriteFile(string relativePath, string destination);
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
