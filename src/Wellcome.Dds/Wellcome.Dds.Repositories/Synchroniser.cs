using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;

namespace Wellcome.Dds.Repositories
{

    public class Synchroniser
    {
        private readonly IMetsRepository metsRepository;
        private readonly ILogger<Synchroniser> logger;
        private readonly DdsContext ddsContext;
        private readonly ICatalogue catalogue;

        public Synchroniser(
            IMetsRepository metsRepository,
            ILogger<Synchroniser> logger,
            DdsContext ddsContext,
            ICatalogue catalogue)
        {
            this.metsRepository = metsRepository;
            this.logger = logger;
            this.ddsContext = ddsContext;
            this.catalogue = catalogue;
        }
        

        public async Task RefreshFlatManifestations(string identifier)
        {
            logger.LogInformation("Synchronising {id}", identifier);
            var isBNumber = identifier.IsBNumber();
                
            List<Manifestation> flatManifestationsForBNumber = null;
            // bool isNew = false;
            int shortB = -1;
            List<int> foundManifestationIndexes = null;
            bool containsRestrictedFiles = false;
            IMetsResource packageMetsResource = null;
            IFileBasedResource packageFileResource = null;
            var work = await catalogue.GetWork(identifier);
            if (isBNumber)
            {
                // operations we can only do when the identifier being processed is a b number
                flatManifestationsForBNumber = ddsContext.Manifestations.Where(
                    fm => fm.PackageIdentifier == identifier && fm.Index >= 0)
                    .ToList();
                // remove any error manifestations, we can recreate them
                var errors = ddsContext.Manifestations.Where(
                    fm => fm.PackageIdentifier == identifier && fm.Index < 0);
                foreach (var error in errors)
                {
                    ddsContext.Manifestations.Remove(error);
                }
                // isNew = !flatManifestationsForBNumber.Any();
                shortB = identifier.ToShortBNumber();
                foundManifestationIndexes = new List<int>();
                packageMetsResource = await metsRepository.GetAsync(identifier);
                packageFileResource = packageMetsResource;
            }

            await foreach (var mic in metsRepository.GetAllManifestationsInContext(identifier))
            {
                var manifestation = mic.Manifestation;
                if (manifestation.Partial)
                {
                    manifestation = (IManifestation) await metsRepository.GetAsync(manifestation.Id);
                }
                if (isBNumber)
                {
                    if (manifestation.ModsData.AccessCondition == "Restricted files")
                    {
                        // TODO: do we want to omit it like this?
                        // Why not just add a "contains restricted files" field to Manifestation?
                        containsRestrictedFiles = true;
                        foundManifestationIndexes = new List<int>();
                        break;
                    }
                    foundManifestationIndexes.Add(mic.SequenceIndex);
                }
                var ddsId = new DdsIdentifier(manifestation.Id);

                var flatManifestationsForIdentifier = ddsContext.Manifestations
                    .Where(fm => fm.PackageIdentifier == ddsId.BNumber && fm.Index == mic.SequenceIndex)
                    .ToArray();

                var flatManifestation = flatManifestationsForIdentifier.FirstOrDefault();
                if (flatManifestationsForIdentifier.Length > 1)
                {
                    foreach (var fm in flatManifestationsForIdentifier.Skip(1))
                    {
                        // more than one manif with same bnumber and seq index
                        ddsContext.Manifestations.Remove(fm);
                        if (isBNumber)
                        {
                            var duplicateFm = flatManifestationsForBNumber.Single(fmd => fmd.Id == fm.Id);
                            flatManifestationsForBNumber.Remove(duplicateFm);
                        }
                    }
                }

                var assets = manifestation.SignificantSequence;

                bool supportsSearch = assets.Any(pf => pf.RelativeAltoPath.HasText());
                var fileSystemResource = manifestation as IFileBasedResource;
                
                // (some Change code removed here) - we're not going to implement this for now


                if (flatManifestation == null)
                {
                    flatManifestation = new Manifestation
                    {
                        Id = ddsId,
                        PackageIdentifier = ddsId.BNumber,
                        PackageShortBNumber = ddsId.BNumber.ToShortBNumber(),
                        Index = mic.SequenceIndex,
                        Label = manifestation.Label.HasText() ? manifestation.Label : "(no label in METS)"
                    };
                    if (packageMetsResource != null)
                    {
                        flatManifestation.PackageLabel = packageMetsResource.Label;
                    }
                    flatManifestation.RootSectionType = manifestation.Type;
                    await ddsContext.Manifestations.AddAsync(flatManifestation);
                }

                flatManifestation.CalmRef = work.GetCalmRef();
                flatManifestation.SupportsSearch = assets.Any(pf => pf.RelativeAltoPath.HasText());
                flatManifestation.IsAllOpen = assets.TrueForAll(pf => pf.AccessCondition == AccessCondition.Open);
                flatManifestation.PermittedOperations = string.Join(",", manifestation.PermittedOperations);
                flatManifestation.RootSectionAccessCondition = manifestation.ModsData.AccessCondition;
                if (assets.HasItems())
                {
                    flatManifestation.FileCount = assets.Count;
                    if (assets.Count > 0)
                    { 
                        var asset = assets[0];
                        flatManifestation.AssetType = manifestation.FirstSignificantInternetType;
                        if (flatManifestation.AssetType == "image/jp2")
                            flatManifestation.AssetType = "seadragon/dzi";
                        flatManifestation.FirstFileStorageIdentifier = asset.StorageIdentifier;

                        flatManifestation.FirstFileExtension =
                            asset.AssetMetadata.GetFileName().GetFileExtension().ToLowerInvariant();
                        flatManifestation.DipStatus = null;
                    }
                }
                if (packageFileResource != null)
                {
                    flatManifestation.PackageFile = packageFileResource.SourceFile.Uri;
                    flatManifestation.PackageFileModified = packageFileResource.SourceFile.LastWriteTime;
                }
                var fsr = (IFileBasedResource) manifestation;
                flatManifestation.ManifestationFile = fsr.SourceFile.Uri;
                flatManifestation.ManifestationFileModified = fsr.SourceFile.LastWriteTime;
                flatManifestation.Processed = DateTime.Now;

                // extra fields that only the new dash knows about
                flatManifestation.DlcsAssetType = manifestation.FirstSignificantInternetType;
                flatManifestation.ManifestationIdentifier = ddsId;
                flatManifestation.VolumeIdentifier = ddsId.IdentifierType == IdentifierType.Volume
                    ? ddsId.VolumePart
                    : null;

            }
            
            if (isBNumber)
            {
                string betterTitle = null;
                if (containsRestrictedFiles)
                {
                    // TODO - not an error, just flag it, we can constrain queries later
                    const string message = "{0} contains restricted files, creating error manifestation";
                    const string dipStatus = "restricted";
                    CreateErrorManifestation(shortB, message, identifier, dipStatus);
                }
                else if (foundManifestationIndexes.Count == 0)
                {
                    const string message = "No manifestations for {0}, creating error manifestation";
                    const string dipStatus = "no-manifs";
                    CreateErrorManifestation(shortB, message, identifier, dipStatus);
                }
                else
                {
                    betterTitle = work.Title;
                    await RefreshMetadata(identifier, work);
                }
                foreach (var flatManifestation in flatManifestationsForBNumber)
                {
                    if (!foundManifestationIndexes.Contains(flatManifestation.Index))
                    {
                        logger.LogInformation("Removing flatManifestation {bnumber}/{index}",
                            flatManifestation.PackageIdentifier, flatManifestation.Index);
                        ddsContext.Manifestations.Remove(flatManifestation);
                    }
                    else
                    {
                        if (betterTitle.HasText())
                        {
                            flatManifestation.PackageLabel = betterTitle;
                        }
                    }
                }
            }
            await ddsContext.SaveChangesAsync();
        }

        
        
        private void CreateErrorManifestation(int shortB, string message, 
            string bNumber, string dipStatus)
        {
            DeleteMetadata(bNumber);
            logger.LogWarning(message, bNumber);
            var fm = new Manifestation
            {
                Id = bNumber,
                PackageIdentifier = bNumber,
                PackageShortBNumber = shortB,
                Index = -1,
                Processed = DateTime.Now,
                DipStatus = dipStatus
            };
            ddsContext.Manifestations.Add(fm);
        }
        
        
        public async Task RefreshMetadata(string identifier, Work work)
        {
            DeleteMetadata(identifier);
            await ddsContext.Metadata.AddRangeAsync(work.GetMetadata());
        }

        private void DeleteMetadata(string identifier)
        {
            var fieldsForIdentifier = ddsContext.Metadata.Where(m => m.ManifestationId == identifier);
            ddsContext.Metadata.RemoveRange(fieldsForIdentifier);
        }
    }
}