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
        private MediaDimensions mediaDimensions;
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

        private const string DefaultMimeType = "application/octet-stream";
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

            return DefaultMimeType;
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

        public int GetImageWidth()
        {
            EnsureMediaDimensions();
            return mediaDimensions.Width.GetValueOrDefault();
        }

        public int GetImageHeight()
        {
            EnsureMediaDimensions();
            return mediaDimensions.Height.GetValueOrDefault();
        }
        
        public double GetDuration()
        {
            EnsureMediaDimensions();
            return mediaDimensions.Duration.GetValueOrDefault();
        }
        
        public string GetDisplayDuration()
        {
            EnsureMediaDimensions();
            return mediaDimensions.DurationDisplay;
        }
        
        private void EnsureMediaDimensions()
        {
            if (mediaDimensions != null)
            {
                return;
            }

            mediaDimensions = new MediaDimensions();
            bool processToolOutputs = false; // If we need to seek this info in Archivematica tool outputs
            var mimeType = GetMimeType();
            if (mimeType.StartsWith("image/") || mimeType.StartsWith("video/") || mimeType == DefaultMimeType)
            {
                // the most common type... and the most common (Goobi) metadata:
                mediaDimensions.Width = GetInt32FilePropertyValue("ImageWidth");
                mediaDimensions.Height = GetInt32FilePropertyValue("ImageHeight");
                if (mediaDimensions.Width is null or <= 0)
                {
                    // Not found in Goobi metadata, so:
                    processToolOutputs = true;
                }
            }

            if (mimeType.StartsWith("video/") || mimeType.StartsWith("audio/") || mimeType == DefaultMimeType)
            {
                // for now look for Goobi way first as it's much quicker.
                // Over time we may expect far more BD AV so we could try the Archivematica way first.
                var durationString = GetFilePropertyValue("Duration");
                if (durationString.HasText())
                {
                    double parsedDuration;
                    if (double.TryParse(durationString, out var result))
                    {
                        parsedDuration = result;
                    }
                    else
                    {
                        parsedDuration = ParseDuration(durationString);
                    }
                    if (parsedDuration > 0)
                    {
                        mediaDimensions.DurationDisplay = durationString;
                        mediaDimensions.Duration = parsedDuration;
                    }
                }
                else
                {
                    processToolOutputs = true;
                }
            }

            if (processToolOutputs)
            {
                // Also for now we won't treat application/* as time-based, or image.
                PopulateMediaDimensionsFromToolOutputs();
            }
        }

        private void PopulateMediaDimensionsFromToolOutputs()
        {
            // SAFPA_C_D_5_15_1 - video/quicktime - ffprobe, one form of Exif
            // SA_REN_B_21_1 - video/x-ms-wmv - FITS Exiftool
            // PPSML_Z_11_4 - video/quicktime - ffprobe as above
            // PPCRI_D_4_5A - jpeg - MediaInfo - track type="Image", same for TIFFs
            
            var objectCharacteristics = premisObjectXElement
                .Descendants(XNames.PremisObjectCharacteristicsExtension)
                .SingleOrDefault();

            if (objectCharacteristics == null)
            {
                return;
            }

            var mediaInfoTracks = objectCharacteristics.Descendants(XNames.MediaInfoTrack).ToList();
            if (mediaInfoTracks.Any())
            {
                foreach (var track in mediaInfoTracks)
                {
                    switch (track.Attribute("type")?.Value)
                    {
                        case "Image":
                            GetWidthAndHeightFromMediaInfoTrack(track);
                            break;
                        case "Video":
                            GetWidthAndHeightFromMediaInfoTrack(track);
                            GetDurationFromMediaInfoTrack(track);
                            break;
                        case "Audio":
                            GetDurationFromMediaInfoTrack(track);
                            break;
                    }
                }
                // return here? Or carry on seeking more info?
            }

            var fitsExifOutput = objectCharacteristics.GetAllDescendantsWithAttribute(
                XNames.FitsTool, "name", "Exiftool").FirstOrDefault();
            if (fitsExifOutput != null)
            {
                // see slack
            }



        }

        private void GetWidthAndHeightFromMediaInfoTrack(XElement track)
        {
            if (mediaDimensions.Width.GetValueOrDefault() <= 0)
            {
                mediaDimensions.Width = track
                    .GetDesendantElementValue(XNames.MediaInfoWidth)
                    .ToNullableInt();
            }

            if (mediaDimensions.Height.GetValueOrDefault() <= 0)
            {
                mediaDimensions.Height = track
                    .GetDesendantElementValue(XNames.MediaInfoHeight)
                    .ToNullableInt();
            }
        }

        private void GetDurationFromMediaInfoTrack(XElement track)
        {
            if (mediaDimensions.Duration.GetValueOrDefault() <= 0)
            {
                mediaDimensions.Duration = track
                    .GetDesendantElementValue(XNames.MediaInfoDuration)
                    .ToNullableDouble();
                mediaDimensions.DurationDisplay = mediaDimensions.Duration.ToString();
            }
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
            // We want this class to work with both kinds of METS, and possibly for Goobi METS to start using more
            // Premis or other information. 
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

    class MediaDimensions
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public double? Duration { get; set; }
        public string DurationDisplay { get; set; }
    }
}
