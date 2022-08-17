using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Util;
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

            // TODO: This won't be true for much longer!
            // This line will just go, I think. We can no longer enforce this test here.
            if (!ddsId.HasBNumber)
            {
                throw new ArgumentException($"{ddsId.PackageIdentifier} is not a b number", nameof(identifier));
            }

            IWorkStore workStore = await workStorageFactory.GetWorkStore(ddsId.PackageIdentifier);
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
            }

            throw new NotSupportedException("Unknown identifier");
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
            var logicalStructMap = metsXml.XElement.GetSingleElementWithAttribute(XNames.MetsStructMap, "TYPE", "LOGICAL");
            var rootStructuralDiv = logicalStructMap.Elements(XNames.MetsDiv).Single(); // require only one
            var structMap = new LogicalStructDiv(rootStructuralDiv, metsXml.RelativeXmlFilePath, identifier, workStore);
            return structMap;
        }
    }
}
