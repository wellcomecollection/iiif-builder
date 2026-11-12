using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model
{
    /// <summary>
    /// This class is only used to model Goobi METS, not Archivematica.
    /// </summary>
    public class LogicalStructDiv : ILogicalStructDiv
    {
        private const char FileSeparator = '\\';
        // What gets turned into a collection and what gets turned into a manifest isn't quite so clear cut
        // Collections
        private const string MultipleManifestation = "MultipleManifestation";
        private const string Periodical = "Periodical";
        private const string PeriodicalVolume = "PeriodicalVolume";

        // Manifests - with caveats
        private const string PeriodicalIssue = "PeriodicalIssue";
        private const string Archive = "Archive";
        private const string Monograph = "Monograph";
        private const string Manuscript = "Manuscript";
        private const string Video = "Video";
        private const string Audio = "Audio";
        private const string Artwork = "Artwork";
        private const string Transcript = "Transcript";
        private const string Map = "Map";

        // dodgy ones:
        private const string MultipleVolume = "MultipleVolume";
        private const string MultipleCopy = "MultipleCopy";
        private const string MultipleVolumeMultipleCopy = "MultipleVolumeMultipleCopy";



        public bool IsCollection => Type is 
            MultipleManifestation or Periodical or PeriodicalVolume;

        public bool IsManifestation =>
            Type is 
                Monograph or 
                Archive or 
                Artwork or 
                Manuscript or 
                PeriodicalIssue or 
                Video or 
                Transcript or 
                MultipleVolume or 
                MultipleCopy or 
                MultipleVolumeMultipleCopy or 
                Audio or 
                Map;

        public IWorkStore WorkStore { get; set; }
        public string Id { get; set; }
        public string? ExternalId { get; set; }
        public string AdmId { get; set; }
        public string DmdId { get; set; }
        public string? RelativeLinkPath { get; set; }
        public string? LinkId { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public int? Order { get; set; }
        public List<ILogicalStructDiv> Children { get; set; }
        public string ContainingFileRelativePath { get; set; }

        private readonly XElement rootElement;

        public override string ToString()
        {
            const string format = "[{4}] ID={0} TYPE=\"{1}\" LABEL=\"{2}\" ORDER={3}";
            return string.Format(format, Id, Type, Label, Order, ExternalId);
        }

        public bool HasChildLink()
        {
            if (string.IsNullOrEmpty(RelativeLinkPath))
            {
                return false;
            }
            var fileName = RelativeLinkPath.Substring(RelativeLinkPath.LastIndexOf(FileSeparator) + 1);
            return fileName.Contains("_"); // Need a better rule than this
        }


        public LogicalStructDiv(XElement div, string containingFileRelativePath, string? externalId, IWorkStore workStore)
        {
            ExternalId = externalId;
            ContainingFileRelativePath = containingFileRelativePath;
            WorkStore = workStore;
            rootElement = div.AncestorsAndSelf().Last();
            // Does the div contain a link to another file?
            var metsPointer = div.Element(XNames.MetsMptr);
            if (metsPointer != null)
            {
                // This might be a pointer back to the anchor file, if we are in a MM
                string path = metsPointer.GetRequiredAttributeValue(XNames.XLinkHref);
                LinkId = path.Chomp(".xml"); // Use this as a prefix to subsequent child manifestations
                if (!path.EndsWith(".xml")) path += ".xml";
                RelativeLinkPath = path;
            }
            Id =    (string) div.Attribute("ID")!;
            AdmId = (string) div.Attribute("ADMID")!;
            DmdId = (string) div.Attribute("DMDID")!;
            Label = (string) div.Attribute("LABEL")!;
            Type =  (string) div.Attribute("TYPE")!;
            Order = SanitiseOrder(div.Attribute("ORDER"));

            Children = div.Elements(XNames.MetsDiv)
                        .Select(md => new LogicalStructDiv(md, containingFileRelativePath, null, workStore) as ILogicalStructDiv)
                        .OrderBy(sd => sd.Order)
                        .ToList();
            // Correct the ExternalId for each of these...
            foreach (var child in Children)
            {
                if (child.HasChildLink())
                {
                    child.ExternalId = child.LinkId;
                }
                else if(child.IsManifestation || child.Type == PeriodicalVolume)
                {
                    child.ExternalId = externalId;
                }
                if (child.Type == PeriodicalVolume)
                {
                    // could also get this from the purposeName in tessella data for the volume
                    foreach (var issue in child.Children)
                    {
                        issue.ExternalId = externalId + issue.Id.RemoveStart("LOG");
                    }
                }
            }
        }

        private int? SanitiseOrder(XAttribute? attribute)
        {
            // Some order labels from Internet Archive workflows look like this: "167402"
            // Where the first 4 digits are the publication year and the second two are the "real" order.
            var asString = (string?) attribute;
            if (asString.HasText() && asString.Length == 6 && asString.All(char.IsDigit))
            {
                return Convert.ToInt32(asString[4..]);
            }

            return (int?) attribute;
        }

        private ModsData? modsData;
        private bool modsLoaded;

        public ISectionMetadata? GetSectionMetadata()
        {
            // Lazy loaded mods
            if (!modsLoaded)
            {
                if (DmdId.HasText())
                {
                    var dmdSec = rootElement.GetSingleElementWithAttribute(XNames.MetsDmdSec, "ID", DmdId);
                    modsData = new ModsData(dmdSec);
                    if (!modsData.DzLicenseCode.HasText())
                    {
                        if (Type.StartsWith("Periodical"))
                        {
                            modsData.DzLicenseCode = "CC-BY-NC";
                        }
                    }
                }
                modsLoaded = true;
            }
            return modsData;
        }

        private List<IPhysicalFile>? physicalFiles;
        private Dictionary<string, XElement>? fileMap; 
         
        public List<IPhysicalFile> GetPhysicalFiles()
        {
            if (physicalFiles == null)
            {
                fileMap = PhysicalFile.MakeFileMap(rootElement);
                var physicalSequenceElement = rootElement
                    .GetSingleElementWithAttribute(XNames.MetsStructMap, "TYPE", "PHYSICAL")
                    .GetSingleElementWithAttribute(XNames.MetsDiv, "TYPE", "physSequence");
                int index = 0;
                physicalFiles = rootElement
                    .Element(XNames.MetsStructLink)!
                    .Elements(XNames.MetsSmLink)
                    .Where(smLink => (string?) smLink.Attribute(XNames.XLinkFrom) == Id)
                    .Select(
                        smLink =>
                            physicalSequenceElement.GetSingleElementWithAttribute(XNames.MetsDiv, "ID",
                                ((string?) smLink.Attribute(XNames.XLinkTo))!))
                    .Select(physFileElement => PhysicalFile.FromDigitisedMets(physFileElement, fileMap, WorkStore))
                    .OrderBy(physicalFile => physicalFile.Order)
                    .ToList();
                foreach (var physicalFile in physicalFiles)
                {
                    physicalFile.Index = index++;
                }
            }
            return physicalFiles;
        }



        /// <summary>
        /// This needs to carry on working for the temporary bagging version, as well as the new "official" version
        /// </summary>
        /// <returns></returns>
        public IStoredFile? GetPosterImage()
        {
            // 1. Legacy migrated poster images
            // See if we have a "bagger" poster image:
            const string posterAmdId = "AMD_POSTER";
            var posterTechMds = rootElement
                .GetAllDescendantsWithAttribute(XNames.MetsTechMD, "ID", posterAmdId);
            if (posterTechMds.Any())
            {
                var sf = new StoredFile
                {
                    WorkStore = WorkStore,
                    AssetMetadata = WorkStore.MakeAssetMetadata(rootElement, posterAmdId),
                    PhysicalFile = null // there is explicitly NO METS PhysicalFile
                };
                // The insertion of /posters here is because we don't have a physical file element
                // to tell us where it actually is. We're not recording this anywhere.
                sf.RelativePath = "posters/" + sf.AssetMetadata.GetFileName();
                return sf;
            }

            // 2. Interim MXF workflow, with poster image part of the same physicalFile sequence
            if (Type == "Video")
            {
                var anImage = physicalFiles!.FirstOrDefault(pf => pf.MimeType!.StartsWith("image"));
                if (anImage != null)
                {
                    return new StoredFile
                    {
                        WorkStore = WorkStore,
                        AssetMetadata = anImage.AssetMetadata,
                        RelativePath = anImage.RelativePath,
                        PhysicalFile = anImage
                    };
                }
            }
            
            // 3. New AV workflow, where PosterImage is a proper file
            var singleFile = physicalFiles!.FirstOrDefault();
            var poster = singleFile?.Files!.FirstOrDefault(f => f.Use == "POSTER");
            return poster;
        }
    }
}

