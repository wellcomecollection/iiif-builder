using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class PremisMetadata : IAssetMetadata
    {
        private XElement premisObject;
        private readonly XElement metsRoot;
        private readonly string admId;
        private bool initialised = false;
        private Dictionary<string, string> significantProperties; 

        public PremisMetadata(XElement metsRoot, string admId)
        {
            // This is a lazy class indeed...
            this.metsRoot = metsRoot;
            this.admId = admId;
        }

        public string GetFileName()
        {
            if (!initialised) Init();
            var oids = premisObject.Elements(XNames.PremisObjectIdentifier);
            foreach (var oid in oids)
            {
                var oidType = oid.GetDesendantElementValue(XNames.PremisObjectIdentifierType);
                if (oidType == "local")
                {
                    return oid.GetDesendantElementValue(XNames.PremisObjectIdentifierValue);
                }
            }
            return null;
        }

        public string GetFolder()
        {
            throw new NotImplementedException();
        }

        public string GetFileSize()
        {
            if (!initialised) Init();
            return premisObject.GetDesendantElementValue(XNames.PremisSize);
        }

        public string GetFormatName()
        {
            if (!initialised) Init();
            return premisObject.GetDesendantElementValue(XNames.PremisFormatName);
        }

        public string GetAssetId()
        {
            throw new NotImplementedException();
        }

        public string GetLengthInSeconds()
        {
            return GetFilePropertyValue("Duration");
        }

        public string GetBitrateKbps()
        {
            return GetFilePropertyValue("Bitrate");
        }

        public int GetNumberOfPages()
        {
            var num = GetInt32FilePropertyValue("PageNumber");
            return num ?? 0;
        }

        public int GetNumberOfImages()
        {
            throw new NotImplementedException();
            var num = GetInt32FilePropertyValue("Number of Images");
            return num ?? 0;
        }

        public int GetImageWidth()
        {
            var num = GetInt32FilePropertyValue("ImageWidth");
            return num ?? 0;
        }

        public int GetImageHeight()
        {
            var num = GetInt32FilePropertyValue("ImageHeight");
            return num ?? 0;
        }

        private string GetFilePropertyValue(string filePropertyName)
        {
            if (!initialised) Init();
            string value;
            significantProperties.TryGetValue(filePropertyName, out value);
            return value;
        }

        public int? GetInt32FilePropertyValue(string filePropertyName)
        {
            int i;
            var fpv = GetFilePropertyValue(filePropertyName);
            // TODO: temporary workaround because some images have floating point values
            try
            {
                i = (int) Convert.ToDouble(fpv);
                return i;
            }
            catch
            {
                return null;
            }

            //if (int.TryParse(fpv, out i))
            //{
            //    return i;
            //}
            //return null;
        }

        private void Init()
        {
            var techMd = metsRoot.GetSingleDescendantWithAttribute(XNames.MetsTechMD, "ID", admId);
            var xmlData = techMd.Descendants(XNames.MetsXmlData).Single();
            premisObject = xmlData.Element(XNames.PremisObject);
            significantProperties = new Dictionary<string, string>();
            if (premisObject == null) return;
            foreach (var sigProp in premisObject.Elements(XNames.PremisSignificantProperties))
            {
                var propType = sigProp.Element(XNames.PremisSignificantPropertiesType).Value;
                var propValue = sigProp.Element(XNames.PremisSignificantPropertiesValue).Value;
                significantProperties[propType] = propValue;
            }
        }
    }
}
