using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    public class PremisMetadata : IAssetMetadata
    {
        private XElement? premisObjectXElement;
        private XElement? premisRightsStatementXElement;
        private MediaDimensions? mediaDimensions;
        private string? mimeType;
        private readonly XElement metsRoot;
        private readonly string admId;
        private bool initialised;
        private Dictionary<string, string>? significantProperties; 

        public PremisMetadata(XElement metsRoot, string admId)
        {
            // This is a lazy class indeed...
            this.metsRoot = metsRoot;
            this.admId = admId;
        }

        private string? originalName;
        public string GetOriginalName()
        {
            if (originalName != null)
            {
                return originalName;
            }
            if (!initialised) Init();
            const string transferPrefix = "%transferDirectory%objects/";
            var value = premisObjectXElement!.GetDescendantElementValue(XNames.PremisOriginalName);
            if (value == null || !value.Contains(transferPrefix))
            {
                throw new NotSupportedException($"Premis original name does not contain transfer prefix: {value}");
            }
            originalName = value.RemoveStart(transferPrefix);
            if (originalName.HasText())
            {
                return originalName;
            }

            throw new InvalidOperationException("Cannot retrieve original name for file");
        }

        private const string DefaultMimeType = "application/octet-stream";
        public string? GetMimeType()
        {
            if (mimeType == null)
            {
                EnsureMimeTypeAndMediaDimensions();
            }
            return mimeType;
            
        }

        public DateTime? GetCreatedDate()
        {
            if (!initialised) Init();
            var createdDateString = premisObjectXElement!.GetDescendantElementValue(XNames.PremisDateCreatedByApplication);
            if(DateTime.TryParse(createdDateString, out var result))
            {
                return result;
            }

            return null;
        }

        public string? GetFileName()
        {
            if (!initialised) Init();
            var objectIDs = premisObjectXElement!.Elements(XNames.PremisObjectIdentifier);
            foreach (var oid in objectIDs)
            {
                // This only works for Goobi METS
                var oidType = oid.GetDescendantElementValue(XNames.PremisObjectIdentifierType);
                if (oidType == "local")
                {
                    return oid.GetDescendantElementValue(XNames.PremisObjectIdentifierValue);
                }
            }
            // didn't find any objectIDs, look for born digital elements
            if (GetOriginalName().HasText())
            {
                return originalName!.GetFileName();
            }
            return null;
        }

        public string GetFolder()
        {
            throw new NotImplementedException();
        }

        public string? GetFileSize()
        {
            if (!initialised) Init();
            return premisObjectXElement!.GetDescendantElementValue(XNames.PremisSize);
        }

        public string? GetFormatName()
        {
            if (!initialised) Init();
            return premisObjectXElement!.GetDescendantElementValue(XNames.PremisFormatName);
        }

        public string? GetFormatVersion()
        {
            if (!initialised) Init();
            return premisObjectXElement!.GetDescendantElementValue(XNames.PremisFormatVersion);
        }

        public string? GetPronomKey()
        {
            if (!initialised) Init();
            return premisObjectXElement!.GetDescendantElementValue(XNames.PremisFormatRegistryKey);
        }

        public string GetAssetId()
        {
            throw new NotImplementedException();
        }

        public int GetImageWidth()
        {
            EnsureMimeTypeAndMediaDimensions();
            return mediaDimensions!.Width.GetValueOrDefault();
        }

        public int GetImageHeight()
        {
            EnsureMimeTypeAndMediaDimensions();
            return mediaDimensions!.Height.GetValueOrDefault();
        }
        
        public double GetDuration()
        {
            EnsureMimeTypeAndMediaDimensions();
            return mediaDimensions!.Duration.GetValueOrDefault();
        }
        
        public string? GetDisplayDuration()
        {
            EnsureMimeTypeAndMediaDimensions();
            return mediaDimensions!.DurationDisplay;
        }

        public MediaDimensions GetMediaDimensions()
        {
            EnsureMimeTypeAndMediaDimensions();
            return mediaDimensions!;
        }

        private void EnsureMimeTypeAndMediaDimensions()
        {
            if (mediaDimensions != null)
            {
                return;
            }
            
            // provisionally get mime type
            var pronomKey = GetPronomKey();
            var map = PronomData.Instance.FormatMap;
            if (pronomKey.HasText() && map.ContainsKey(pronomKey))
            {
                mimeType = map[pronomKey];
            }

            if (mimeType.IsNullOrEmpty())
            {
                mimeType = DefaultMimeType;
            }

            mediaDimensions = new MediaDimensions();
            bool processToolOutputs = false; // If we need to seek this info in Archivematica tool outputs
            if (mimeType.IsImageMimeType() || mimeType.IsVideoMimeType() || mimeType == DefaultMimeType)
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

            if (mimeType.IsVideoMimeType() || mimeType.IsAudioMimeType() || mimeType == DefaultMimeType)
            {
                // for now look for Goobi way first as it's much quicker.
                // Over time we may expect far more BD AV so we could try the Archivematica way first.
                var durationString = GetFilePropertyValue("Duration");
                if (durationString.HasText())
                {
                    var parsedDuration = ParseDuration(durationString);
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

            RefineMimeType(pronomKey);
        }

        /// <summary>
        /// Some formats can be either audio or video, and the mime type we have picked
        /// from the PRONOM lookup may be wrong.
        /// </summary>
        private void RefineMimeType(string? pronomKey)
        {
            // the first version of this method is going to be explicit - we can come back and 
            // generalise it with more samples.
            switch (pronomKey)
            {
                case "fmt/199":
                {
                    // https://www.nationalarchives.gov.uk/PRONOM/fmt/199
                    int width = mediaDimensions!.Width.GetValueOrDefault();
                    int height = mediaDimensions.Height.GetValueOrDefault();
                    if (width == 0 || height == 0)
                    {
                        mimeType = "audio/mp4"; // will replace video/mp4
                    }

                    break;
                }
                case "x-fmt/183":
                {
                    if (long.TryParse(GetFileSize(), out var result))
                    {
                        if (result > 512)
                        {
                            return;
                        }
                    }
                    // This either has no length, or is suspiciously short, so we're going to
                    // reassign its mime type, which will have it treated as File.
                    mimeType = DefaultMimeType;
                    break;
                }
            }
        }

        private void PopulateMediaDimensionsFromToolOutputs()
        {
            // SAFPA_C_D_5_15_1 - video/quicktime - ffprobe, one form of Exif
            // SA_REN_B_21_1 - video/x-ms-wmv - FITS Exiftool
            // PPSML_Z_11_4 - video/quicktime - ffprobe as above
            // PPCRI_D_4_5A - jpeg - MediaInfo - track type="Image", same for TIFFs
            // GRLDUR_A_6_1 - lots of examples!!!
            
            var objectCharacteristics = premisObjectXElement!
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
                string? widthValue = null;
                string? heightValue = null;
                
                // The fields that hold w,h,d information will be different for different media types.
                // This code will need updating as we encounter more examples

                Dictionary<string, double> foundDurations = new Dictionary<string, double>();
                if (mediaDimensions!.Duration.GetValueOrDefault() > 0)
                {
                    // we may already have found one earlier
                    foundDurations.Add(mediaDimensions!.DurationDisplay!, mediaDimensions.Duration.GetValueOrDefault());
                }
                
                var durationCandidates = new[] { "PlayDuration", "Duration", "LastTimeStamp"};
                foreach (var elementName in durationCandidates)
                {
                    var stringDuration = fitsExifOutput.GetDescendantElementValue(elementName);
                    if (stringDuration.HasText())
                    {
                        var parsedDuration = ParseDuration(stringDuration);
                        if (parsedDuration > 0)
                        {
                            foundDurations.Add(stringDuration, parsedDuration);
                        }
                    }
                }

                if (foundDurations.Count > 0)
                {
                    var longest = foundDurations.MaxBy(kvp => kvp.Value);
                    
                    mediaDimensions.DurationDisplay = longest.Key;
                    mediaDimensions.Duration = longest.Value;
                }

                var imageWidth = fitsExifOutput.GetDescendantElementValue("ImageWidth");
                var imageHeight = fitsExifOutput.GetDescendantElementValue("ImageHeight");
                if (imageWidth.HasText())
                {
                    widthValue = imageWidth;
                }

                if (imageHeight.HasText())
                {
                    heightValue = imageHeight;
                }
               
                if(widthValue.HasText() && heightValue.HasText())
                {
                    if (int.TryParse(widthValue, out var pw))
                    {
                        mediaDimensions.Width = pw;
                    }
                    if (int.TryParse(heightValue, out var ph))
                    {
                        mediaDimensions.Height = ph;
                    }
                }
            }
        }

        private void GetWidthAndHeightFromMediaInfoTrack(XElement track)
        {
            if (mediaDimensions!.Width.GetValueOrDefault() <= 0)
            {
                mediaDimensions.Width = track
                    .GetDescendantElementValue(XNames.MediaInfoWidth)
                    .ToNullableInt();
            }

            if (mediaDimensions.Height.GetValueOrDefault() <= 0)
            {
                mediaDimensions.Height = track
                    .GetDescendantElementValue(XNames.MediaInfoHeight)
                    .ToNullableInt();
            }
        }

        private void GetDurationFromMediaInfoTrack(XElement track)
        {
            if (mediaDimensions!.Duration.GetValueOrDefault() <= 0)
            {
                mediaDimensions.Duration = track
                    .GetDescendantElementValue(XNames.MediaInfoDuration)
                    .ToNullableDouble();
                mediaDimensions.DurationDisplay = mediaDimensions.Duration.ToString() + "s";
            }
        }


        public string GetBitrateKbps()
        {
            return GetFilePropertyValue("Bitrate");
        }

        public int GetNumberOfPages()
        {
            // TODO: we can get this for Archivematica files too but not from this property.
            var num = GetInt32FilePropertyValue("PageNumber");
            return num ?? 0;
        }

        public int GetNumberOfImages()
        {
            throw new NotImplementedException();
        }

        private string? GetFilePropertyValue(string filePropertyName)
        {
            if (!initialised) Init();
            significantProperties!.TryGetValue(filePropertyName, out var value);
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

        private IRightsStatement? rightsStatement;
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
                    AccessCondition = Common.AccessCondition.Missing,
                    Statement = "No Rights"
                };
            }

            if (rightsStatement != null)
            {
                return rightsStatement;
            }

            var accessCondition =
                premisRightsStatementXElement!.GetDescendantElementValue(XNames.PremisRightsGrantedNote);

            if (!Common.AccessCondition.IsValid(accessCondition))
            {
                accessCondition = Common.AccessCondition.Unknown;
            }
            rightsStatement = new PremisRightsStatement
            {
                Identifier = premisRightsStatementXElement!.GetDescendantElementValue(XNames.PremisRightsStatementIdentifier),
                Basis = premisRightsStatementXElement!.GetDescendantElementValue(XNames.PremisRightsBasis),
                AccessCondition = accessCondition
            };

            switch (rightsStatement.Basis)
            {
                case "License":
                    rightsStatement.Statement = premisRightsStatementXElement!.GetDescendantElementValue(XNames.PremisLicenseNote);
                    break;
                case "Copyright":
                    rightsStatement.Statement = premisRightsStatementXElement!.GetDescendantElementValue(XNames.PremisCopyrightNote);
                    rightsStatement.Status = premisRightsStatementXElement!.GetDescendantElementValue(XNames.PremisCopyrightStatus);
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
            XElement? techMd = null;
            XElement? rightsMd = null;
            
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
                var propType = sigProp.Element(XNames.PremisSignificantPropertiesType)!.Value;
                var propValue = sigProp.Element(XNames.PremisSignificantPropertiesValue)!.Value;
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
            if (possibleStringLength.IsNullOrWhiteSpace())
            {
                return 0;
            }

            var test = possibleStringLength.Trim();
            if (test.Contains("mn"))
            {
                // Examples
                // 22mn 49s
                // 1mn 41s
                // 9mn 46s ... this format seems very consistent
                var parts = test.Split(' ');
                int.TryParse(parts[0].ToNumber(), out var mins);
                int.TryParse(parts[1].ToNumber(), out var secs);
                return 60 * mins + secs;
            }

            if (test.Contains(":"))
            {
                var parts = test.Split(':');
                if (parts.Length == 2)
                {
                    test = "00:" + test;
                }
                // TODO - deal with overflow parts only if we find them, e.g., 00:80:20
                if (TimeSpan.TryParse(test, out var ts))
                {
                    // 12:30:33 and similar formats
                    if (ts.TotalSeconds > 0)
                    {
                        return ts.TotalSeconds;
                    }
                }
            }
            
            // "58s"
            if (test.EndsWith("s"))
            {
                test = test.Chomp("s");
            }
            if (double.TryParse(test, out var result))
            {
                return result;
            }

            return 0;
        }
    }
}
