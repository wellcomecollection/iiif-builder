using System.Xml.Linq;

namespace Wellcome.Dds.AssetDomain
{
    public class XmlSource
    {
        public XmlSource(XElement element, string relativeXmlFilePath)
        {
            XElement = element;
            RelativeXmlFilePath = relativeXmlFilePath;
        }

        public XElement XElement { get; set; }
        public string RelativeXmlFilePath{ get; set; }
    }
}
