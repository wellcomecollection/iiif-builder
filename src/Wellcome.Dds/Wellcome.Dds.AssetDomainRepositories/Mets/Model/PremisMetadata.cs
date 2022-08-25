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

        public string GetOriginalName()
        {
            if (!initialised) Init();
            const string transferPrefix = "%transferDirectory%objects/";
            var value = premisObject.GetDesendantElementValue(XNames.PremisOriginalName);
            if (value == null || !value.Contains(transferPrefix))
            {
                throw new NotSupportedException($"Premis original name does not contain transfer prefix: {value}");
            }
            return value.RemoveStart(transferPrefix);
        }

        public string GetMimeType()
        {
            if (!initialised) Init();
            var objectCharacteristics =
                premisObject.Descendants(XNames.PremisObjectCharacteristicsExtension).SingleOrDefault();
            if (objectCharacteristics != null)
            {
                return objectCharacteristics.Descendants(XNames.FitsIdentity)
                    .FirstOrDefault()?
                    .Attribute("mimetype")?
                    .Value;
            }

            return "unknown/unknown";
        }

        public DateTime? GetCreatedDate()
        {
            if (!initialised) Init();
            var createdDateString = premisObject.GetDesendantElementValue(XNames.PremisDateCreatedByApplication);
            if(DateTime.TryParse(createdDateString, out var result))
            {
                return result;
            }

            return null;
        }

        public string GetFileName()
        {
            // This only works for Goobi METS
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

        public string GetFormatVersion()
        {
            if (!initialised) Init();
            return premisObject.GetDesendantElementValue(XNames.PremisFormatVersion);
        }

        public string GetPronomKey()
        {
            if (!initialised) Init();
            return premisObject.GetDesendantElementValue(XNames.PremisFormatRegistryKey);
        }

        public string GetAssetId()
        {
            throw new NotImplementedException();
        }

        public string GetLengthInSeconds()
        {
            return GetFilePropertyValue("Duration");
        }

        public double GetDuration()
        {
            var possibleStringLength = GetLengthInSeconds();
            if (double.TryParse(possibleStringLength, out var result))
            {
                return result;
            }

            return ParseDuration(possibleStringLength);
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
            // Goobi and Archivematica METS are quite differently arranged.
            XElement techMd = null;
            try
            {
                // Goobi layout
                techMd = metsRoot.GetSingleDescendantWithAttribute(XNames.MetsTechMD, "ID", admId);
            }
            catch
            {
                // Archivematic layout
                var amdSec = metsRoot.GetSingleElementWithAttribute(XNames.MetsAmdSec, "ID", admId);
                techMd = amdSec.Element(XNames.MetsTechMD);
            }

            if (techMd == null)
            {
                throw new NotSupportedException($"Unable to locate techMD section for {admId}");
            }
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
        
        /// <summary>
        /// Attempt to parse a duration in seconds from EXIF-derived values.
        /// </summary>
        /// <param name="possibleStringLength">the human readable string</param>
        /// <returns>The length in seconds, or 0 if no length obtained.</returns>
        public static double ParseDuration(string possibleStringLength)
        {
            if (possibleStringLength.HasText())
            {
                // Examples
                // 22mn 49s
                // 1mn 41s
                // 9mn 46s ... this format seems very consistent
                if (possibleStringLength.Contains("mn"))
                {
                    var parts = possibleStringLength.Split(' ');
                    int.TryParse(parts[0].ToNumber(), out var mins);
                    int.TryParse(parts[1].ToNumber(), out var secs);
                    return 60 * mins + secs;
                }
            }

            return 0;
        }
    }
}
