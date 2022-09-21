﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class PremisMetadata : IAssetMetadata
    {
        private XElement premisObjectXElement;
        private XElement premisRightsStatementXElement;
        private readonly XElement metsRoot;
        private readonly string admId;
        private bool initialised;
        private Dictionary<string, string> significantProperties; 

        public PremisMetadata(XElement metsRoot, string admId)
        {
            // This is a lazy class indeed...
            this.metsRoot = metsRoot;
            this.admId = admId;
        }

        private string originalName;
        public string GetOriginalName()
        {
            if (originalName != null)
            {
                return originalName;
            }
            if (!initialised) Init();
            const string transferPrefix = "%transferDirectory%objects/";
            var value = premisObjectXElement.GetDesendantElementValue(XNames.PremisOriginalName);
            if (value == null || !value.Contains(transferPrefix))
            {
                throw new NotSupportedException($"Premis original name does not contain transfer prefix: {value}");
            }
            originalName = value.RemoveStart(transferPrefix);
            return originalName;
        }

        public string GetMimeType()
        {
            string mimeType = null;
            var pronomKey = GetPronomKey();
            var map = PronomData.Instance.FormatMap;
            if (pronomKey.HasText() && map.ContainsKey(pronomKey))
            {
                mimeType = map[pronomKey];
            }

            if (mimeType.HasText())
            {
                return mimeType;
            }
            
            // probably not going to succeed but let's look elsewhere for mime info
            // The FITS section is present on some files. This could be extended to look in other sections.
            
            var objectCharacteristics =
                premisObjectXElement.Descendants(XNames.PremisObjectCharacteristicsExtension).SingleOrDefault();
            if (objectCharacteristics != null)
            {
                // THIS IS NOT RELIABLE
                // See https://digirati.slack.com/archives/CBT40CMKQ/p1662485191979289
                var mimeTypeFromFits = objectCharacteristics.Descendants(XNames.FitsIdentity)
                    .FirstOrDefault()?
                    .Attribute("mimetype")?
                    .Value;
                if (mimeTypeFromFits.HasText())
                {
                    return mimeTypeFromFits;
                }
            }

            return "application/octet-stream";
        }

        public DateTime? GetCreatedDate()
        {
            if (!initialised) Init();
            var createdDateString = premisObjectXElement.GetDesendantElementValue(XNames.PremisDateCreatedByApplication);
            if(DateTime.TryParse(createdDateString, out var result))
            {
                return result;
            }

            return null;
        }

        public string GetFileName()
        {
            if (!initialised) Init();
            var objectIDs = premisObjectXElement.Elements(XNames.PremisObjectIdentifier);
            foreach (var oid in objectIDs)
            {
                // This only works for Goobi METS
                var oidType = oid.GetDesendantElementValue(XNames.PremisObjectIdentifierType);
                if (oidType == "local")
                {
                    return oid.GetDesendantElementValue(XNames.PremisObjectIdentifierValue);
                }
            }
            // didn't find any objectIDs, look for born digital elements
            if (GetOriginalName().HasText())
            {
                return originalName.GetFileName();
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
            return premisObjectXElement.GetDesendantElementValue(XNames.PremisSize);
        }

        public string GetFormatName()
        {
            if (!initialised) Init();
            return premisObjectXElement.GetDesendantElementValue(XNames.PremisFormatName);
        }

        public string GetFormatVersion()
        {
            if (!initialised) Init();
            return premisObjectXElement.GetDesendantElementValue(XNames.PremisFormatVersion);
        }

        public string GetPronomKey()
        {
            if (!initialised) Init();
            return premisObjectXElement.GetDesendantElementValue(XNames.PremisFormatRegistryKey);
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

        private int? GetInt32FilePropertyValue(string filePropertyName)
        {
            var fpv = GetFilePropertyValue(filePropertyName);
            // TODO: temporary workaround because some images have floating point values
            try
            {
                var i = (int) Convert.ToDouble(fpv);
                return i;
            }
            catch
            {
                return null;
            }
        }

        private IRightsStatement rightsStatement;
        public IRightsStatement GetRightsStatement()
        {
            if (!initialised) Init();
            if (premisRightsStatementXElement == null)
            {
                //throw new NotSupportedException(
                //    $"No rights statement found for physical file {physicalFile.Id} in {workStore.Identifier}");
            
                // We need to throw the error above but for now, for testing, we'll make a pseudo-rights statement:
                rightsStatement = new PremisRightsStatement
                {
                    Basis = "No Rights Statement",
                    Identifier = "no-rights",
                    AccessCondition = Common.AccessCondition.Closed,
                    Statement = "No Rights"
                };
            }

            if (rightsStatement != null)
            {
                return rightsStatement;
            }

            rightsStatement = new PremisRightsStatement
            {
                Identifier = premisRightsStatementXElement.GetDesendantElementValue(XNames.PremisRightsStatementIdentifier),
                Basis = premisRightsStatementXElement.GetDesendantElementValue(XNames.PremisRightsBasis),
                AccessCondition = premisRightsStatementXElement.GetDesendantElementValue(XNames.PremisRightsGrantedNote)
            };

            switch (rightsStatement.Basis)
            {
                case "License":
                    rightsStatement.Statement = premisRightsStatementXElement.GetDesendantElementValue(XNames.PremisLicenseNote);
                    break;
                case "Copyright":
                    rightsStatement.Statement = premisRightsStatementXElement.GetDesendantElementValue(XNames.PremisCopyrightNote);
                    rightsStatement.Status = premisRightsStatementXElement.GetDesendantElementValue(XNames.PremisCopyrightStatus);
                    break;
                default:
                    throw new NotSupportedException($"Unknown rights statement basis: {rightsStatement.Basis}");
            }

            return rightsStatement;
        }

        private void Init()
        {
            // Goobi and Archivematica METS are quite differently arranged.
            XElement techMd = null;
            XElement rightsMd = null;
            
            // first try the Goobi layout, as this is the more common:
            var rootTechMDs = metsRoot.GetAllDescendantsWithAttribute(
                XNames.MetsTechMD, "ID", admId).ToList();
            if (rootTechMDs.Any())
            {
                techMd = rootTechMDs.First();
                // There is no rightsMD in Goobi METS
            }
            else
            {
                // Archivematica layout
                var amdSec = metsRoot.GetSingleElementWithAttribute(XNames.MetsAmdSec, "ID", admId);
                techMd = amdSec.Element(XNames.MetsTechMD);
                rightsMd = amdSec.Element(XNames.MetsRightsMD);
            }

            if (techMd == null)
            {
                throw new NotSupportedException($"Unable to locate techMD section for {admId}");
            }
            premisObjectXElement = techMd.Descendants(XNames.MetsXmlData).Single().Element(XNames.PremisObject);
            
            significantProperties = new Dictionary<string, string>();
            if (premisObjectXElement == null) return;
            foreach (var sigProp in premisObjectXElement.Elements(XNames.PremisSignificantProperties))
            {
                var propType = sigProp.Element(XNames.PremisSignificantPropertiesType).Value;
                var propValue = sigProp.Element(XNames.PremisSignificantPropertiesValue).Value;
                significantProperties[propType] = propValue;
            }
            
            if (rightsMd != null)
            {
                premisRightsStatementXElement = rightsMd.Descendants(XNames.MetsXmlData).Single().Element(XNames.PremisRightsStatement);
            }

            initialised = true;
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
