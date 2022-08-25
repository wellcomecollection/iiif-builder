using System.Xml.Linq;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    /// <summary>
    /// METS vocabulary
    /// </summary>
    public static class XNames
    {
        // ReSharper disable All InconsistentNaming
        public static readonly XNamespace mets = "http://www.loc.gov/METS/";
        public static readonly XName MetsDiv = mets + "div";
        public static readonly XName MetsStructMap = mets + "structMap";
        public static readonly XName MetsStructLink = mets + "structLink";
        public static readonly XName MetsSmLink = mets + "smLink";
        public static readonly XName MetsFileSec = mets + "fileSec";
        public static readonly XName MetsFile = mets + "file";
        public static readonly XName MetsMptr = mets + "mptr";
        public static readonly XName MetsAmdSec = mets + "amdSec";
        public static readonly XName MetsDmdSec = mets + "dmdSec";
        public static readonly XName MetsMdWrap = mets + "mdWrap";
        public static readonly XName MetsXmlData = mets + "xmlData";
        public static readonly XName MetsFptr = mets + "fptr";
        public static readonly XName MetsFLocat = mets + "FLocat";
        public static readonly XName MetsTechMD = mets + "techMD";

        public static readonly XNamespace mods = "http://www.loc.gov/mods/v3";
        public static readonly XName ModsTitle = mods + "title";
        public static readonly XName ModsSubTitle = mods + "subTitle";
        public static readonly XName ModsOriginPublisher = mods + "originPublisher";
        public static readonly XName ModsPublisher = mods + "publisher";
        public static readonly XName ModsPlaceTerm = mods + "placeTerm";
        public static readonly XName ModsClassification = mods + "classification";
        public static readonly XName ModsLanguageTerm = mods + "languageTerm";
        public static readonly XName ModsRecordIdentifier = mods + "recordIdentifier";
        public static readonly XName ModsIdentifier = mods + "identifier";
        public static readonly XName ModsPhysicalDescription = mods + "physicalDescription";
        public static readonly XName ModsDisplayForm = mods + "displayForm";
        public static readonly XName ModsAccessCondition = mods + "accessCondition";
        public static readonly XName ModsNote = mods + "note";
        public static readonly XName ModsDateIssued = mods + "dateIssued";
        public static readonly XName ModsNumber = mods + "number";
        public static readonly XName ModsPart = mods + "part";

        public static readonly XNamespace xlink = "http://www.w3.org/1999/xlink";
        public static readonly XName XLinkFrom = xlink + "from";
        public static readonly XName XLinkTo = xlink + "to";
        public static readonly XName XLinkHref = xlink + "href";

        public static readonly XNamespace tessella = "http://www.tessella.com/transfer";
        public static readonly XName TessellaID = tessella + "ID";
        public static readonly XName TessellaFile = tessella + "File";
        public static readonly XName TessellaFileName = tessella + "FileName";
        public static readonly XName TessellaFileSize = tessella + "FileSize";
        public static readonly XName TessellaFormatName = tessella + "FormatName";
        public static readonly XName TessellaFolder = tessella + "Folder";
        public static readonly XName TessellaFilePropertyName = tessella + "FilePropertyName";
        public static readonly XName TessellaValue = tessella + "Value";

        public static readonly XNamespace premis = "http://www.loc.gov/premis/v3";
        public static readonly XName PremisObject = premis + "object";
        public static readonly XName PremisObjectIdentifier = premis + "objectIdentifier";
        public static readonly XName PremisObjectIdentifierType = premis + "objectIdentifierType";
        public static readonly XName PremisObjectIdentifierValue = premis + "objectIdentifierValue";
        public static readonly XName PremisSignificantProperties = premis + "significantProperties";
        public static readonly XName PremisSignificantPropertiesType = premis + "significantPropertiesType";
        public static readonly XName PremisSignificantPropertiesValue = premis + "significantPropertiesValue";
        public static readonly XName PremisSize = premis + "size";
        public static readonly XName PremisFormatName = premis + "formatName";
        public static readonly XName PremisFormatVersion = premis + "formatVersion";
        public static readonly XName PremisFormatRegistryKey = premis + "formatRegistryKey";
        public static readonly XName PremisOriginalName = premis + "originalName";
        public static readonly XName PremisDateCreatedByApplication = premis + "dateCreatedByApplication";
        public static readonly XName PremisObjectCharacteristicsExtension = premis + "objectCharacteristicsExtension";

        public static readonly XNamespace fits = "http://hul.harvard.edu/ois/xml/ns/fits/fits_output";
        public static readonly XName FitsIdentity = fits + "identity";

        public static readonly XNamespace wt = "http://wellcome.ac.uk/";
        public static readonly XName WtVolumeNumber = wt + "volumeNumber";
        public static readonly XName WtCopyNumber = wt + "copyNumber";
    }
}
