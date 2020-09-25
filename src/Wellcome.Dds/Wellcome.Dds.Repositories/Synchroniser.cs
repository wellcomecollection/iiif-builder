using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Presentation;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;

namespace Wellcome.Dds.Repositories
{

    public class Synchroniser
    {
        private readonly IMetsRepository metsRepository;
        private readonly ILogger<Synchroniser> logger;
        private readonly DdsContext ddsContext;
        private readonly ICatalogue catalogue;
        private readonly DlcsOptions dlcsOptions;
        private readonly UriPatterns uriPatterns;
        
        // Similarly, this is looking to match thumbnails in the Catalogue API, 
        // which at some point will change to iiif.wc.org
        private readonly Regex thumbRegex = new Regex(@"https://dlcs\.io/thumbs/wellcome/[0-9]*/([^/]*)/full/.*");

        public Synchroniser(
            IMetsRepository metsRepository,
            ILogger<Synchroniser> logger,
            DdsContext ddsContext,
            ICatalogue catalogue,
            IOptions<DlcsOptions> dlcsOptions,
            UriPatterns uriPatterns)
        {
            this.metsRepository = metsRepository;
            this.logger = logger;
            this.ddsContext = ddsContext;
            this.catalogue = catalogue;
            this.dlcsOptions = dlcsOptions.Value;
            this.uriPatterns = uriPatterns;
        }
        

        public async Task RefreshDdsManifestations(string identifier, Work work = null)
        {
            logger.LogInformation("Synchronising {id}", identifier);
            var isBNumber = identifier.IsBNumber();
                
            List<Manifestation> ddsManifestationsForBNumber = null;
            // bool isNew = false;
            var shortB = -1;
            List<int> foundManifestationIndexes = null;
            var containsRestrictedFiles = false;
            IMetsResource packageMetsResource = null;
            IFileBasedResource packageFileResource = null;
            work ??= await catalogue.GetWorkByOtherIdentifier(identifier);
            if (work == null)
            {
                throw new ArgumentException($"Work in Synchroniser cannot be null - {identifier}", nameof(work));
            }
            if (isBNumber)
            {
                // operations we can only do when the identifier being processed is a b number
                ddsManifestationsForBNumber = ddsContext.Manifestations.Where(
                    fm => fm.PackageIdentifier == identifier && fm.Index >= 0)
                    .ToList();
                // remove any error manifestations, we can recreate them
                var errors = ddsContext.Manifestations.Where(
                    fm => fm.PackageIdentifier == identifier && fm.Index < 0);
                foreach (var error in errors)
                {
                    ddsContext.Manifestations.Remove(error);
                }
                // isNew = !ddsManifestationsForBNumber.Any();
                shortB = identifier.ToShortBNumber();
                foundManifestationIndexes = new List<int>();
                packageMetsResource = await metsRepository.GetAsync(identifier);
                packageFileResource = packageMetsResource;
            }

            await foreach (var mic in metsRepository.GetAllManifestationsInContext(identifier))
            {
                var metsManifestation = mic.Manifestation;
                if (metsManifestation.Partial)
                {
                    metsManifestation = (IManifestation) await metsRepository.GetAsync(metsManifestation.Id);
                }
                if (isBNumber)
                {
                    if (metsManifestation.ModsData.AccessCondition == "Restricted files")
                    {
                        // TODO: do we want to omit it like this?
                        // Why not just add a "contains restricted files" field to Manifestation?
                        containsRestrictedFiles = true;
                        foundManifestationIndexes = new List<int>();
                        break;
                    }
                    foundManifestationIndexes.Add(mic.SequenceIndex);
                }
                var ddsId = new DdsIdentifier(metsManifestation.Id);

                var ddsManifestationsForIdentifier = ddsContext.Manifestations
                    .Where(fm => fm.PackageIdentifier == ddsId.BNumber && fm.Index == mic.SequenceIndex)
                    .ToArray();

                var ddsManifestation = ddsManifestationsForIdentifier.FirstOrDefault();
                if (ddsManifestationsForIdentifier.Length > 1)
                {
                    foreach (var fm in ddsManifestationsForIdentifier.Skip(1))
                    {
                        // more than one manif with same bnumber and seq index
                        ddsContext.Manifestations.Remove(fm);
                        if (isBNumber)
                        {
                            var duplicateFm = ddsManifestationsForBNumber.Single(fmd => fmd.Id == fm.Id);
                            ddsManifestationsForBNumber.Remove(duplicateFm);
                        }
                    }
                }

                var assets = metsManifestation.SignificantSequence;
                
                // (some Change code removed here) - we're not going to implement this for now

                if (ddsManifestation == null)
                {
                    ddsManifestation = new Manifestation
                    {
                        Id = ddsId,
                        PackageIdentifier = ddsId.BNumber,
                        PackageShortBNumber = ddsId.BNumber.ToShortBNumber(),
                        Index = mic.SequenceIndex,
                        Label = metsManifestation.Label.HasText() ? metsManifestation.Label : "(no label in METS)"
                    };
                    if (packageMetsResource != null)
                    {
                        ddsManifestation.PackageLabel = packageMetsResource.Label;
                    }
                    ddsManifestation.RootSectionType = metsManifestation.Type;
                    await ddsContext.Manifestations.AddAsync(ddsManifestation);
                }

                ddsManifestation.WorkId = work.Id;
                ddsManifestation.WorkType = work.WorkType.Id;
                ddsManifestation.ReferenceNumber = work.ReferenceNumber;
                ddsManifestation.CalmRef = work.GetIdentifierByType("calm-ref-no");
                ddsManifestation.CalmAltRef = work.GetIdentifierByType("calm-altref-no");
                if (ddsManifestation.CalmRef.HasText())
                {
                    ddsManifestation.CollectionReferenceNumber = ddsManifestation.CalmRef;
                    var parentWorkId = work.GetParentId();
                    if (parentWorkId.HasText())
                    {
                        var immediateParent = await catalogue.GetWorkByWorkId(parentWorkId);
                        if (immediateParent != null)
                        {
                            ddsManifestation.CalmRefParent = immediateParent.GetIdentifierByType("calm-ref-no");
                            ddsManifestation.CalmAltRefParent = immediateParent.GetIdentifierByType("calm-altref-no");
                        }
                    }
                }
                // The following walk up the tree is currently only possible for archives, but we won't put it
                // in the above condition because it could become available for other types of work later

                var parent = work;
                while (parent.PartOf.HasItems())
                {
                    parent = parent.PartOf.Last();
                }
                ddsManifestation.CollectionReferenceNumber = parent.ReferenceNumber;
                
                ddsManifestation.SupportsSearch = assets.Any(pf => pf.RelativeAltoPath.HasText());
                ddsManifestation.IsAllOpen = assets.TrueForAll(pf => pf.AccessCondition == AccessCondition.Open);
                ddsManifestation.PermittedOperations = string.Join(",", metsManifestation.PermittedOperations);
                ddsManifestation.RootSectionAccessCondition = metsManifestation.ModsData.AccessCondition;
                if (assets.HasItems())
                {
                    ddsManifestation.FileCount = assets.Count;
                    if (assets.Count > 0)
                    { 
                        var asset = assets[0];
                        ddsManifestation.AssetType = metsManifestation.FirstSignificantInternetType;
                        // Drop use of pseudo seadragon/dzi type - this will need attention elsewhere
                        // if (ddsManifestation.AssetType == "image/jp2")
                        //     ddsManifestation.AssetType = "seadragon/dzi";
                        ddsManifestation.FirstFileStorageIdentifier = asset.StorageIdentifier;
                        ddsManifestation.FirstFileExtension =
                            asset.AssetMetadata.GetFileName().GetFileExtension().ToLowerInvariant();
                        ddsManifestation.DipStatus = null;
                        switch (ddsManifestation.AssetType.GetAssetFamily())
                        {
                            case AssetFamily.Image:
                                ddsManifestation.FirstFileThumbnailDimensions = asset.GetAvailableSizeAsString();
                                ddsManifestation.FirstFileThumbnail = GetDlcsThumbnailServiceForAsset(asset);
                                if (ddsManifestation.Index == 0)
                                {
                                    // the first manifestation; add in the thumb from the catalogue, too
                                    IPhysicalFile catThumbAsset = GetPhysicalFileFromThumbnailPath(work, assets);
                                    if (catThumbAsset != null)
                                    {
                                        ddsManifestation.CatalogueThumbnailDimensions = catThumbAsset.GetAvailableSizeAsString();
                                        ddsManifestation.CatalogueThumbnail = GetDlcsThumbnailServiceForAsset(catThumbAsset);
                                    }
                                }
                                break;
                            case AssetFamily.TimeBased:
                                ddsManifestation.FirstFileThumbnailDimensions =
                                    asset.AssetMetadata.GetLengthInSeconds();
                                break;
                        }
                    }
                }
                if (packageFileResource != null)
                {
                    ddsManifestation.PackageFile = packageFileResource.SourceFile.Uri;
                    ddsManifestation.PackageFileModified = packageFileResource.SourceFile.LastWriteTime;
                }
                var fsr = (IFileBasedResource) metsManifestation;
                ddsManifestation.ManifestationFile = fsr.SourceFile.Uri;
                ddsManifestation.ManifestationFileModified = fsr.SourceFile.LastWriteTime;
                ddsManifestation.Processed = DateTime.Now;

                // extra fields that only the new dash knows about
                ddsManifestation.DlcsAssetType = metsManifestation.FirstSignificantInternetType;
                ddsManifestation.ManifestationIdentifier = ddsId;
                ddsManifestation.VolumeIdentifier = ddsId.IdentifierType == IdentifierType.Volume
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
                foreach (var ddsManifestation in ddsManifestationsForBNumber)
                {
                    if (!foundManifestationIndexes.Contains(ddsManifestation.Index))
                    {
                        logger.LogInformation("Removing ddsManifestation {bnumber}/{index}",
                            ddsManifestation.PackageIdentifier, ddsManifestation.Index);
                        ddsContext.Manifestations.Remove(ddsManifestation);
                    }
                    else
                    {
                        if (betterTitle.HasText())
                        {
                            ddsManifestation.PackageLabel = betterTitle;
                        }
                    }
                }
            }
            await ddsContext.SaveChangesAsync();
            
        }

        private IPhysicalFile GetPhysicalFileFromThumbnailPath(Work work, List<IPhysicalFile> assets)
        {
            if (work.Thumbnail == null) return null;
            var match = thumbRegex.Match(work.Thumbnail.Url);
            if (!match.Success) return null;
            var storageIdentifier = match.Groups[1].Value;
            return assets.FirstOrDefault(a => a.StorageIdentifier == storageIdentifier);
        }

        private string GetDlcsThumbnailServiceForAsset(IPhysicalFile asset)
        {
            return uriPatterns.DlcsThumb(dlcsOptions.CustomerDefaultSpace, asset.StorageIdentifier);
        }
        
        // private string GetDlcsImageServiceForAsset(IPhysicalFile asset)
        // {
        //     return ImageServiceTemplate
        //         .Replace("{space}", dlcsOptions.CustomerDefaultSpace.ToString())
        //         .Replace("{id}", asset.StorageIdentifier);
        // }
    

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
            await ddsContext.Metadata.AddRangeAsync(work.GetMetadata(identifier));
        }

        private void DeleteMetadata(string identifier)
        {
            var fieldsForIdentifier = ddsContext.Metadata.Where(m => m.ManifestationId == identifier);
            ddsContext.Metadata.RemoveRange(fieldsForIdentifier);
        }
    }
}