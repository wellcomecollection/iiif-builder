using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Wellcome.Dds.Common;
using MetsManifestation = Wellcome.Dds.AssetDomainRepositories.Mets.Model.Manifestation;

namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class MetsRepository : IMetsRepository
    {
        private readonly IWorkStorageFactory workStorageFactory;
        private readonly ILogger<MetsRepository> logger;
        
        // For born-digital
        private const string Directory = "Directory";
        private const string Item = "Item";
        private const string TypeAttribute = "TYPE";
        private const string LabelAttribute = "LABEL";

        public MetsRepository(
            IWorkStorageFactory workStorageFactory,
            ILogger<MetsRepository> logger)
        {
            this.workStorageFactory = workStorageFactory;
            this.logger = logger;
        }

        public async Task<IMetsResource> GetAsync(string identifier)
        {
            // forms:
            // b12345678 - could be an anchor file or a single manifestation work. 
            // Returns ICollection or IManifestation

            // b12345678_XXX - could be a manifestation, or a Periodical Volume
            // Returns ICollection or IManifestation

            // b12345678_XXX_YYY - Can only be a periodical issue at the moment
            // Returns IManifestation

            // b12345678/0 - old form, must be an IManifestation
            var ddsId = new DdsIdentifier(identifier);

            IWorkStore workStore = await workStorageFactory.GetWorkStore(ddsId);
            ILogicalStructDiv structMap;
            switch (ddsId.IdentifierType)
            {
                case IdentifierType.BNumber:
                    structMap = await GetFileStructMap(ddsId.BNumber, workStore);
                    return GetMetsResource(structMap, workStore);
                case IdentifierType.Volume:
                    structMap = await GetLinkedStructMapAsync(ddsId.VolumePart, workStore);
                    return GetMetsResource(structMap, workStore);
                case IdentifierType.BNumberAndSequenceIndex:
                    return await GetMetsResourceByIndex(ddsId.BNumber, ddsId.SequenceIndex, workStore);
                case IdentifierType.Issue:
                    structMap = await GetLinkedStructMapAsync(ddsId.VolumePart, workStore);
                    // we only want a specific issue
                    var issueStruct = structMap.Children.Single(c => c.ExternalId == identifier);
                    return new MetsManifestation(issueStruct, structMap);
                
                case IdentifierType.NonBNumber:
                    var bdManifestation = await BuildBornDigitalManifestation(workStore);
                    return bdManifestation;
            }

            throw new NotSupportedException("Unknown identifier");
        }

        private async Task<IManifestation> BuildBornDigitalManifestation(IWorkStore workStore)
        {
            // we can't get a logical struct map, because there isn't one in this METS.
            // But there is one in the mets file in the submission...
            // https://digirati.slack.com/archives/CBT40CMKQ/p1649945431278779
            var metsXml = await workStore.LoadRootDocumentXml();
            var physicalStructMap = metsXml.XElement.GetSingleElementWithAttribute(XNames.MetsStructMap, TypeAttribute, "physical");
            // https://digirati.slack.com/archives/CBT40CMKQ/p1661272044683399
            var rootDir = physicalStructMap.GetSingleElementWithAttribute(XNames.MetsDiv, TypeAttribute, Directory);
            var objectsDir = rootDir.GetSingleElementWithAttribute(XNames.MetsDiv, TypeAttribute, Directory);
            // There can be only one
            if (objectsDir?.Attribute(LabelAttribute)?.Value != "objects")
            {
                throw new NotSupportedException("Could not find objects directory in physical structMap");
            }

            // These should be the last two, but we won't mind the order
            XElement metadataDirectory = null;
            XElement submissionDocumentationDirectory = null;

            var objectsChildren = objectsDir.Elements().ToArray();
            if (objectsChildren.Length >= 2)
            {
                metadataDirectory = objectsChildren[^2..].SingleOrDefault(el =>
                    el.Attribute(TypeAttribute)?.Value == Directory && el.Attribute(LabelAttribute)?.Value == "metadata");
                submissionDocumentationDirectory = objectsChildren[^2..].SingleOrDefault(el =>
                    el.Attribute(TypeAttribute)?.Value == Directory && el.Attribute(LabelAttribute)?.Value == "submissionDocumentation");
            }

            if (metadataDirectory == null || submissionDocumentationDirectory == null)
            {
                throw new NotSupportedException("Objects directory does not have metadata and submissionDocumentation as last two entries");
            }

            var subLabel = submissionDocumentationDirectory.Elements().First().Attribute(LabelAttribute)?.Value;
            // not sure if we need this yet. It has the logical structMap.
            var submissionMetsRelativePath = $"submissionDocumentation/{subLabel}/METS.xml";
            
            // We can now ignore the last two.
            var digitalContent = objectsChildren[..^2];
            if (digitalContent.Length == 0)
            {
                throw new NotSupportedException("The objects directory has no digital content");
            }
            
            
            
            // Now note the metadata and submissionDocumentation directories as last two children of objects 
            // get the path to the METS for submission doc if we need it later - it has the logical structmap
            // from the physical Directory and Item information, build both the physicalFile list and the structures.
            
            // build an IManifestation.
            // We might need to pull some info out of LogicalStructDiv if we are duplicating.

            // In Goobi METS, the logical structmap is the root of all navigation and model building.
            // But here, the logical structMap is less important, because the physical structmap conveys the
            // directory structure anyway and there's not anything more "real world" to model (unlike parts of books).
            
            // Notes about accessconditions and related:
            // https://digirati.slack.com/archives/CBT40CMKQ/p1648716914566629
            // https://digirati.slack.com/archives/CBT40CMKQ/p1648211809211439

            // assume still true:
            // https://digirati.slack.com/archives/CBT40CMKQ/p1648717080923719
            
            var fileMap = PhysicalFile.MakeFileMap(metsXml.XElement);
            
            // we're going to build the physical file list and the structural information at the same time,
            // as we walk the directory structure in the physical structMap.
            
            // We'll populate these on the BD manifestation:
            // public List<IPhysicalFile> Sequence { get; set; }
            // public List<IStoredFile> SynchronisableFiles { get; }
            // public IStructRange RootStructRange { get; set; }
            var bdm = new BornDigitalManifestation
            {
                // Many props still to assigned 
                Label = workStore.Identifier, // we have no descriptive metadata!
                Id = workStore.Identifier,
                Type = "Born Digital",
                Order = 0,
                Sequence = new List<IPhysicalFile>(),
                IgnoredStorageIdentifiers = new List<string>(),
                RootStructRange = new StructRange
                {
                    Label = "objects",
                    Type = Directory,
                    PhysicalFileIds = new List<string>()
                },
                SourceFile = workStore.GetFileInfoForPath(workStore.GetRootDocument())
            };
            // all our structRanges are going to be directories
            AddDirectoryToBornDigitalManifestation(
                metsXml.XElement,
                fileMap,
                bdm.Sequence,
                bdm.RootStructRange,
                digitalContent, 
                workStore);
            
            // Chars permitted in CALM ref
            // https://digirati.slack.com/archives/CBT40CMKQ/p1649768933875669
            
            for (int index = 0; index < bdm.Sequence.Count; index++)
            {
                bdm.Sequence[index].Index = index;
                bdm.Sequence[index].Order = index + 1;
                bdm.Sequence[index].OrderLabel = (index + 1).ToString();
            }

            
            return bdm;
        }

        private void AddDirectoryToBornDigitalManifestation(
            XElement rootElement,
            Dictionary<string, XElement> fileMap,
            List<IPhysicalFile> physicalFiles,
            IStructRange structRange,
            XElement[] contents,
            IWorkStore workStore)
        {
            bool hasSeenDirectory = false;
            foreach (var element in contents)
            {
                var label = element.Attribute(LabelAttribute)?.Value;
                // directories then folders within a directory
                if (element.Attribute(TypeAttribute)?.Value == Item)
                {
                    if (hasSeenDirectory)
                    {
                        // https://digirati.slack.com/archives/CBT40CMKQ/p1661348387002749
                        throw new NotSupportedException(
                            $"Encountered a file (Item) after processing a directory: {label}");
                    }
                    var fileId = element.Elements(XNames.MetsFptr).First().Attribute("FILEID")?.Value;
                    if (fileId.IsNullOrWhiteSpace())
                    {
                        throw new NotSupportedException(
                            $"File has no pointer to file element: {label}");
                    }

                    var file = fileMap[fileId];
                    var physicalFile = PhysicalFile.FromBornDigitalMets(rootElement, file, workStore);
                    physicalFiles.Add(physicalFile);
                    structRange.PhysicalFileIds.Add(physicalFile.Id);

                 
                } 
                else if (element.Attribute(TypeAttribute)?.Value == Directory)
                {
                    hasSeenDirectory = true;
                    // make another structure, then call this recursively.
                }
            }
        }

        public async IAsyncEnumerable<IManifestationInContext> GetAllManifestationsInContext(string identifier)
        {
            logger.LogInformation($"Get all manifestations in context for {identifier}", identifier);
            var rootMets = await GetAsync(identifier);
            int sequenceIndex = 0;
            if (rootMets is IManifestation mets)
            {
                var ddsId = new DdsIdentifier(identifier);
                string volumeIdentifier = null, issueIdentifier = null;
                switch (ddsId.IdentifierType)
                {
                    case IdentifierType.Volume:
                        volumeIdentifier = identifier;
                        sequenceIndex = await FindSequenceIndex(ddsId);
                        break;
                    case IdentifierType.Issue:
                        volumeIdentifier = ddsId.VolumePart;
                        issueIdentifier = identifier;
                        sequenceIndex = await FindSequenceIndex(ddsId);
                        break;
                }
                yield return new ManifestationInContext
                {
                    Manifestation = mets,
                    BNumber = ddsId.BNumber,
                    SequenceIndex = sequenceIndex,
                    VolumeIdentifier = volumeIdentifier,
                    IssueIdentifier = issueIdentifier
                };
            }

            if (rootMets is ICollection rootCollection)
            {
                if (rootMets.Type == "Periodical")
                {
                    foreach (var partialVolume in rootCollection.Collections)
                    {
                        var volume = await GetAsync(partialVolume.Id) as ICollection;
                        Debug.Assert(volume != null, "volume != null");
                        foreach (var manifestation in volume.Manifestations)
                        {
                            yield return new ManifestationInContext
                            {
                                Manifestation = manifestation,
                                BNumber = identifier,
                                SequenceIndex = sequenceIndex++,
                                VolumeIdentifier = volume.Id,
                                IssueIdentifier = manifestation.Id
                            };
                        }
                    }
                }
                else
                {
                    foreach (var manifestation in rootCollection.Manifestations)
                    {
                        yield return new ManifestationInContext
                        {
                            Manifestation = manifestation,
                            BNumber = identifier,
                            SequenceIndex = sequenceIndex++,
                            VolumeIdentifier = manifestation.Id,
                            IssueIdentifier = null
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Old format:
        /// b12345678/0 or b12345678/3421 (indexed position)
        /// 
        /// This finds the indexed position b12345678/n
        /// 
        /// Obviously performs rather badly the further you go into C and D - it has to count logical struct divs
        /// </summary>
        /// <param name="bNumber"></param>
        /// <param name="index"></param>
        /// <param name="workStore"></param>
        /// <returns></returns>
        private async Task<IMetsResource> GetMetsResourceByIndex(string bNumber, int index, IWorkStore workStore)
        {
            // 
            var structMap = await GetFileStructMap(bNumber, workStore);
            if (structMap.IsManifestation)
            {
                return GetMetsResource(structMap, workStore);
            }
            // an anchor file...
            if (structMap.Type != "Periodical")
            {
                var child = structMap.Children[index];
                if (child.IsManifestation)
                {
                    structMap = await GetLinkedStructMapAsync(child.LinkId, workStore);
                    return GetMetsResource(structMap, workStore);
                }
                return null;
            }
            // A volume. This is trickier! need to count all the other ones
            int counter = 0;
            foreach (var structDiv in structMap.Children)
            {
                var pdVolume = await GetLinkedStructMapAsync(structDiv.LinkId, workStore);
                foreach (var pdIssue in pdVolume.Children)
                {
                    if (counter++ == index)
                    {
                        return new MetsManifestation(pdIssue);
                    }
                }
            }
            return null;
        }

        public async Task<int> FindSequenceIndex(string identifier)
        {
            int sequenceIndex = 0;
            var ddsId = new DdsIdentifier(identifier);
            switch (ddsId.IdentifierType)
            {
                case IdentifierType.BNumber:
                case IdentifierType.NonBNumber:
                    return 0;
                case IdentifierType.Volume:
                    var anchor = await GetAsync(ddsId.PackageIdentifier) as ICollection;
                    if (anchor == null) return -1;
                    foreach (var manifestation in anchor.Manifestations)
                    {
                        if (manifestation.Id == identifier)
                        {
                            return sequenceIndex;
                        }
                        sequenceIndex++;
                    }
                    return -1;
                case IdentifierType.BNumberAndSequenceIndex:
                    throw new ArgumentException("Identifier already assumes sequence index");
                case IdentifierType.Issue:
                    return -1; // No finding issues by sequence index ANY MORE
                    // throw new NotSupportedException("No finding issues by sequence index ANY MORE");
                    // return GetCachedIssueSequenceIndex(ddsId);
            }

            throw new NotSupportedException("Unknown identifier");
        }

        private async Task<ILogicalStructDiv> GetFileStructMap(string identifier, IWorkStore workStore)
        {
            var metsXml = await workStore.LoadXmlForIdentifier(identifier);
            return GetLogicalStructDiv(metsXml, identifier, workStore);
        }
        
     private static IMetsResource GetMetsResource(ILogicalStructDiv structMap, IWorkStore workStore)
        {
            IMetsResource res = null;
            if (structMap.IsManifestation)
            {
                res = new MetsManifestation(structMap);
            }
            else if (structMap.IsCollection)
            {
                var coll = new Collection(structMap);
                if (structMap.Type == "Periodical")
                {
                    coll.Collections = new List<ICollection>();
                    foreach (var volumeStructMap in structMap.Children)
                    {
                        // This approach yields a FULL issue, no partial vols or manifestations:
                        //var linkedVolume = GetLinkedStructMap(volumeStructMap.LinkId, workStore);
                        //var volumeColl = new Collection(linkedVolume);
                        //coll.Collections.Add(volumeColl);
                        //var issues = linkedVolume.Children.Select(c => new Manifestation(c));
                        //volumeColl.Manifestations = new List<IManifestation>(issues);

                        // This approach just yields named child collections. Callers will need
                        // to identify volumes as partial, and go back to the repository to get
                        // the full volume. 
                        // volumeColl.Manifestations will be null.
                        var volumeColl = new Collection(volumeStructMap);
                        coll.Collections.Add(volumeColl);
                    }
                }
                else
                {
                    var children = structMap.Children.Select(c => new MetsManifestation(c));
                    coll.Manifestations = new List<IManifestation>(children);
                }
                res = coll;
            }
            return res;
        }

        private static async Task<ILogicalStructDiv> GetLinkedStructMapAsync(string mmIdentifier, IWorkStore workStore)
        {
            var metsXml = await workStore.LoadXmlForIdentifier(mmIdentifier);
            var structMap = GetLogicalStructDiv(metsXml, mmIdentifier, workStore);
            // Move to first child (the root structMap is the MM container, the anchor)
            return structMap.Children.First();
        }

        private static ILogicalStructDiv GetLogicalStructDiv(XmlSource metsXml, string identifier, IWorkStore workStore)
        {
            var logicalStructMap = metsXml.XElement.GetSingleElementWithAttribute(XNames.MetsStructMap, TypeAttribute, "LOGICAL");
            var rootStructuralDiv = logicalStructMap.Elements(XNames.MetsDiv).Single(); // require only one
            var structMap = new LogicalStructDiv(rootStructuralDiv, metsXml.RelativeXmlFilePath, identifier, workStore);
            return structMap;
        }
    }
}
