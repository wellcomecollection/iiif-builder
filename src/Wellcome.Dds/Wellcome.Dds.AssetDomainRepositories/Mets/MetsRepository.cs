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

        public async Task<IMetsResource?> GetAsync(DdsIdentifier identifier)
        {
            // forms:
            // b12345678 - could be an anchor file or a single manifestation work. 
            // Returns ICollection or IManifestation

            // b12345678_XXX - could be a manifestation, or a Periodical Volume
            // Returns ICollection or IManifestation

            // b12345678_XXX_YYY - Can only be a periodical issue at the moment
            // Returns IManifestation

            // b12345678/0 - old form, must be an IManifestation
            IWorkStore workStore = await workStorageFactory.GetWorkStore(identifier);
            ILogicalStructDiv structMap;
            switch (identifier.IdentifierType)
            {
                case IdentifierType.BNumber:
                    structMap = await GetFileStructMap(identifier.BNumber!, workStore);
                    return GetMetsResource(structMap);
                case IdentifierType.Volume:
                    structMap = await GetLinkedStructMapAsync(identifier.VolumePart!, workStore);
                    return GetMetsResource(structMap);
                case IdentifierType.BNumberAndSequenceIndex:
                    return await GetMetsResourceByIndex(identifier.BNumber!, identifier.SequenceIndex, workStore);
                case IdentifierType.Issue:
                    structMap = await GetLinkedStructMapAsync(identifier.VolumePart!, workStore);
                    // we only want a specific issue
                    var issueStruct = structMap.Children.Single(c => c.ExternalId == identifier.ToString());
                    return new MetsManifestation(issueStruct, structMap);
                
                case IdentifierType.NonBNumber:
                    var bdManifestation = await BuildBornDigitalManifestation(workStore);
                    return bdManifestation;
            }

            throw new NotSupportedException("Unknown identifier");
        }

        private async Task<IManifestation?> BuildBornDigitalManifestation(IWorkStore workStore)
        {
            // we can't get a logical struct map, because there isn't one in this METS.
            // But there is one in the mets file in the submission...
            // https://digirati.slack.com/archives/CBT40CMKQ/p1649945431278779
            
            // for now we won't use this METS, we'll see if we can get everything we need from the root METS
            var metsXml = await workStore.LoadRootDocumentXml();
            var physicalStructMap = metsXml.XElement.GetSingleElementWithAttribute(XNames.MetsStructMap, TypeAttribute, "physical");
            var rootDir = physicalStructMap.GetSingleElementWithAttribute(XNames.MetsDiv, TypeAttribute, Directory);
            
            // The files we are interested in are in /objects
            // This folder contains the files and folders of the archive, and also two preservation artefacts,
            // the directories /metadata and /submissionDocumentation.
            // We ignore these - they are not part of the deliverable digital object.
            // https://digirati.slack.com/archives/CBT40CMKQ/p1661272044683399
            // However, we will throw an exception if they are not present, because that means something is wrong.
            var objectsDir = rootDir.GetSingleElementWithAttribute(XNames.MetsDiv, TypeAttribute, Directory);
            // There can be only one
            if (objectsDir.Attribute(LabelAttribute)?.Value != "objects")
            {
                throw new NotSupportedException("Could not find objects directory in physical structMap");
            }
            // These should be the last two, but we won't mind the order
            XElement? metadataDirectory = null;
            XElement? submissionDocumentationDirectory = null;
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

            // Get the path to the METS for submission doc if we need it later - it has the logical structMap
            // var subLabel = submissionDocumentationDirectory.Elements().First().Attribute(LabelAttribute)?.Value;
            // var submissionMetsRelativePath = $"submissionDocumentation/{subLabel}/METS.xml";
            
            // We can now ignore the last two.
            var digitalContent = objectsChildren[..^2];
            if (digitalContent.Length == 0)
            {
                throw new NotSupportedException("The objects directory has no digital content");
            }
            
            // In Goobi METS, the logical structmap is the root of all navigation and model building.
            // But here, the logical structMap is less important, because the physical structmap conveys the
            // directory structure anyway and there's not anything more "real world" to model (unlike parts of books).
            
            // Notes about access conditions and related:
            // https://digirati.slack.com/archives/CBT40CMKQ/p1648716914566629
            // https://digirati.slack.com/archives/CBT40CMKQ/p1648211809211439

            // assume still true:
            // https://digirati.slack.com/archives/CBT40CMKQ/p1648717080923719
            
            var fileMap = PhysicalFile.MakeFileMap(metsXml.XElement);
            
            // we're going to build the physical file list and the structural information at the same time,
            // as we walk the directory structure in the physical structMap.
            
            var objectsStructRange = new StructRange
            {
                Label = "objects",
                Type = Directory,
                PhysicalFileIds = new List<string>()
            };
            
            var bdm = new BornDigitalManifestation
            {
                // Many props still to assigned 
                Label = workStore.Identifier, // we have no descriptive metadata!
                Identifier = workStore.Identifier,
                Type = "Born Digital",
                Order = 0,
                Sequence = new List<IPhysicalFile>(),
                IgnoredStorageIdentifiers = new List<string>(),
                RootStructRange = objectsStructRange,
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
            
            // Now assign order and labels to each of the PhysicalFiles
            for (int index = 0; index < bdm.Sequence.Count; index++)
            {
                bdm.Sequence[index].Index = index;
                bdm.Sequence[index].Order = index + 1;
                bdm.Sequence[index].OrderLabel = (index + 1).ToString();
            }

            bdm.PhysicalFileMap = bdm.Sequence.ToDictionary(pf => pf.Id);
            
            DecorateStructure(bdm.RootStructRange, bdm.PhysicalFileMap);
            ConvertIdsToPaths(bdm.RootStructRange, null);
            var objectsMetadata = bdm.RootStructRange.SectionMetadata;
            bdm.SectionMetadata = new BornDigitalSectionMetadata
            {
                Title = bdm.Label,
                AccessCondition = objectsMetadata!.AccessCondition,
                DzLicenseCode = objectsMetadata.DzLicenseCode
            };
            return bdm;
        }

        /// <summary>
        /// Work out the original folder names, from their files, and add structural
        /// access and rights information. Initially this is just based on the first file
        /// in the section.
        /// </summary>
        /// <param name="structRange"></param>
        /// <param name="fileMap"></param>
        private void DecorateStructure(
            IStructRange structRange,
            Dictionary<string, IPhysicalFile>? fileMap)
        {
            // Replace the labels obtained from the <mets:div TYPE="Directory" /> with 
            // labels derived from the originalName path, but use the METS labels to generate
            // a path-based ID for the structRange. Some structRanges don't have files.
            if (structRange.PhysicalFileIds.HasItems())
            {
                var firstFileId = structRange.PhysicalFileIds.First();
            
                if (firstFileId.HasText())
                {
                    var file = fileMap![firstFileId];
                    // this assumes that the originalName always uses / as separator
                    var parts = file.OriginalName!.Split('/');
                    if (parts.Length > 1)
                    {
                        var label = parts[^2];
                        if (label.HasText())
                        {
                            structRange.Label = label;
                        }
                    }
                    structRange.SectionMetadata = new BornDigitalSectionMetadata
                    {
                        Title = structRange.Label,
                        AccessCondition = file.AccessCondition,
                        DzLicenseCode = file.AssetMetadata!.GetRightsStatement().Statement
                    };
                }
            }

            if (!structRange.Children.HasItems()) return;
            
            foreach (var childStructRange in structRange.Children)
            {
                DecorateStructure(childStructRange, fileMap);
                if (structRange.SectionMetadata == null)
                {
                    // this happens when the directory had no immediate child files
                    // which means it MUST have child folders
                    var firstChildStructRange = structRange.Children.First();
                    structRange.SectionMetadata = new BornDigitalSectionMetadata
                    {
                        Title = structRange.Label,
                        AccessCondition = firstChildStructRange.SectionMetadata!.AccessCondition,
                        DzLicenseCode = firstChildStructRange.SectionMetadata.DzLicenseCode
                    };
                }
            }
        }

        /// <summary>
        /// In the first pass the structRanges were assigned the folder name as ID.
        /// While this is the path-safe form and not the original folder name, it's
        /// not guaranteed unique within the object. We need to replace the folder name
        /// IDs with full paths.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="parentRange"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ConvertIdsToPaths(IStructRange range, IStructRange? parentRange)
        {
            if (parentRange != null && parentRange.Id.HasText())
            {
                range.Id = parentRange.Id + "/" + range.Id;
            }

            if (range.Children.IsNullOrEmpty()) return; 

            foreach (var childRange in range.Children)
            {
                ConvertIdsToPaths(childRange, range);
            }
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
                        
                        logger.LogWarning($"Encountered a file (Item) after processing a directory: {label}");
                        // throw new NotSupportedException(..);
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
                    structRange.PhysicalFileIds!.Add(physicalFile.Id);

                 
                } 
                else if (element.Attribute(TypeAttribute)?.Value == Directory)
                {
                    hasSeenDirectory = true;
                    // make another structure, then call this recursively.
                    // Generate IDs for the ranges on the second pass through.
                    var childStructRange = new StructRange
                    {
                        // This is the path-safe Label Attribute; we'll need to replace this with
                        // the original folder name, but we also use it to generate the path-safe Range ID
                        Label = label, 
                        Id = label,
                        Type = Directory,
                        PhysicalFileIds = new List<string>()
                    };
                    structRange.Children ??= new List<IStructRange>();
                    structRange.Children.Add(childStructRange);
                    var childContents = element.Elements().ToArray();
                    AddDirectoryToBornDigitalManifestation(
                        rootElement, fileMap, physicalFiles,
                        childStructRange, childContents, workStore
                        );
                }
            }
        }

        public async IAsyncEnumerable<IManifestationInContext> GetAllManifestationsInContext(DdsIdentifier identifier)
        {
            logger.LogInformation($"Get all manifestations in context for {identifier}", identifier);
            var rootMets = await GetAsync(identifier);
            int sequenceIndex = 0;
            if (rootMets is IManifestation mets)
            {
                string? volumeIdentifier = null, issueIdentifier = null;
                switch (identifier.IdentifierType)
                {
                    case IdentifierType.Volume:
                        volumeIdentifier = identifier;
                        sequenceIndex = await FindSequenceIndex(identifier);
                        break;
                    case IdentifierType.Issue:
                        volumeIdentifier = identifier.VolumePart;
                        issueIdentifier = identifier;
                        sequenceIndex = await FindSequenceIndex(identifier);
                        break;
                }
                yield return new ManifestationInContext(mets, identifier.PackageIdentifier)
                {
                    SequenceIndex = sequenceIndex,
                    VolumeIdentifier = volumeIdentifier,
                    IssueIdentifier = issueIdentifier
                };
            }

            if (rootMets is ICollection rootCollection)
            {
                if (rootMets.Type == "Periodical")
                {
                    foreach (var partialVolume in rootCollection.Collections!)
                    {
                        var volume = await GetAsync(partialVolume.Identifier!) as ICollection;
                        Debug.Assert(volume != null, "volume != null");
                        foreach (var manifestation in volume.Manifestations!)
                        {
                            yield return new ManifestationInContext(manifestation, identifier)
                            {
                                SequenceIndex = sequenceIndex++,
                                VolumeIdentifier = volume.Identifier!,
                                IssueIdentifier = manifestation.Identifier!
                            };
                        }
                    }
                }
                else
                {
                    foreach (var manifestation in rootCollection.Manifestations!)
                    {
                        yield return new ManifestationInContext(manifestation, identifier)
                        {
                            SequenceIndex = sequenceIndex++,
                            VolumeIdentifier = manifestation.Identifier!,
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
        private async Task<IMetsResource?> GetMetsResourceByIndex(string bNumber, int index, IWorkStore workStore)
        {
            // 
            var structMap = await GetFileStructMap(bNumber, workStore);
            if (structMap.IsManifestation)
            {
                return GetMetsResource(structMap);
            }
            // an anchor file...
            if (structMap.Type != "Periodical")
            {
                var child = structMap.Children[index];
                if (child.IsManifestation)
                {
                    if (child.LinkId.IsNullOrWhiteSpace())
                    {
                        throw new InvalidOperationException("An anchor file must have links to further METS files");
                    }
                    structMap = await GetLinkedStructMapAsync(child.LinkId, workStore);
                    return GetMetsResource(structMap);
                }
                return null;
            }
            // A volume. This is trickier! need to count all the other ones
            int counter = 0;
            foreach (var structDiv in structMap.Children)
            {
                if (structDiv.LinkId.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("An anchor file must have links to further METS files");
                }
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

        public async Task<int> FindSequenceIndex(DdsIdentifier identifier)
        {
            int sequenceIndex = 0;
            switch (identifier.IdentifierType)
            {
                case IdentifierType.BNumber:
                case IdentifierType.NonBNumber:
                    return 0;
                case IdentifierType.Volume:
                    var anchor = await GetAsync(identifier.PackageIdentifier) as ICollection;
                    if (anchor == null) return -1;
                    foreach (var manifestation in anchor.Manifestations!)
                    {
                        if (manifestation.Identifier == identifier)
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
        
        private static IMetsResource? GetMetsResource(ILogicalStructDiv structMap)
        {
            IMetsResource? res = null;
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
