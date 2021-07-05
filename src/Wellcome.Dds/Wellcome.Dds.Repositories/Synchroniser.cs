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
        private readonly Regex dlcsThumbRegex = new Regex(@"https://dlcs\.io/thumbs/wellcome/[0-9]*/([^/]*)/full/.*");
        private readonly Regex wcorgThumbRegex = new Regex(@"https://iiif\.wellcomecollection\.org/thumbs/([^/]*)/full/.*");
        // ^^^^^
        // Don't forget this! Needs to happen in Catalogue API *after* we go live.
        // ***********************
        
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
        

        /// <summary>
        /// We keep a Manifestation row for each individual Manifestation - a 6 vol work has 6 rows
        /// Manifestations may get added or removed, their file counts may change, etc.
        ///
        /// The id of a row is always the METS-derived Manifestation ID, which for a single volume work is a b number,
        /// and for a volume in a multi-volume work will be something like b12121212_0003.
        ///
        /// This method will always process ALL manifestations, if you give it a b number.
        ///
        /// This method will re-create the metadata used for IIIF collection aggregations, if you pass a b number.
        /// </summary>
        /// <param name="identifier">
        /// Either the manifestation identifier, or a bnumber. This is the same for single vol works, with the same
        /// effect, but for multiple volumes, passing the b number will create/update rows for all of the volumes
        /// </param>
        /// <param name="work">
        /// The work from the Catalogue API. Often the caller already has this for other reasons, so can pass it here
        /// for efficiency. If null, this method will try to obtain it from the catalogue.
        /// </param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task RefreshDdsManifestations(string identifier, Work? work = null)
        {
            logger.LogInformation("Synchronising {id}", identifier);
            var isBNumber = identifier.IsBNumber();
            var shortB = -1;
            var workBNumber = new DdsIdentifier(identifier).BNumber;
            var manifestationIdsProcessed = new List<string>();
            var containsRestrictedFiles = false;
            IMetsResource? packageMetsResource = null;
            IFileBasedResource? packageFileResource = null;
            work ??= await catalogue.GetWorkByOtherIdentifier(workBNumber);
            if (work == null)
            {
                throw new ArgumentException($"Work in Synchroniser cannot be null - {workBNumber}", nameof(work));
            }
            if (isBNumber)
            {
                // operations we can only do when the identifier being processed is a b number
                
                // remove any error manifestations, we can recreate them
                var errors = ddsContext.Manifestations
                    .Where(fm => fm.PackageIdentifier == identifier && fm.Index < 0);
                foreach (var error in errors)
                {
                    ddsContext.Manifestations.Remove(error);
                }
                await ddsContext.SaveChangesAsync();
                
                shortB = identifier.ToShortBNumber();
                logger.LogInformation("Getting METS resource for synchroniser: {identifier}", identifier);
                packageMetsResource = await metsRepository.GetAsync(identifier);
                packageFileResource = packageMetsResource;
            }

            // At this point no uncommitted DB state
            
            // Get all the manifestations for this identifier. If the identifier is a b number, it will be
            // all possible manifestations currently defined in METS for this b number.
            await foreach (var mic in metsRepository.GetAllManifestationsInContext(identifier))
            {
                var metsManifestation = mic.Manifestation;
                var ddsId = new DdsIdentifier(metsManifestation.Id);
                if (metsManifestation.Partial)
                {
                    logger.LogInformation("Getting individual manifestation for synchroniser: {identifier}", metsManifestation.Id);
                    metsManifestation = (IManifestation) await metsRepository.GetAsync(metsManifestation.Id);
                }
                if (isBNumber)
                {
                    if (metsManifestation.ModsData.AccessCondition == "Restricted files")
                    {
                        containsRestrictedFiles = true;
                    }
                    manifestationIdsProcessed.Add(ddsId);
                }

                var ddsManifestation = await ddsContext.Manifestations.FindAsync(ddsId.ToString());
                var assets = metsManifestation.Sequence;
                
                // (some Change code removed here) - we're not going to implement this for now
                if (ddsManifestation == null)
                {
                    ddsManifestation = new Manifestation
                    {
                        Id = ddsId,
                        PackageIdentifier = ddsId.BNumber,
                        PackageShortBNumber = ddsId.BNumber.ToShortBNumber(),
                        Index = mic.SequenceIndex
                    };
                    if (packageMetsResource != null)
                    {
                        ddsManifestation.PackageLabel = packageMetsResource.Label;
                    }
                    ddsManifestation.RootSectionType = metsManifestation.Type;
                    
                    // The instance of entity type 'Manifestation' cannot be tracked because another
                    // instance with the same key value for {'Id'} is already being tracked. 
                    await ddsContext.Manifestations.AddAsync(ddsManifestation);
                }

                ddsManifestation.Label =
                    metsManifestation.Label.HasText() ? metsManifestation.Label : "(no label in METS)";
                ddsManifestation.WorkId = work.Id;
                ddsManifestation.WorkType = work.WorkType.Id;
                ddsManifestation.ReferenceNumber = work.ReferenceNumber;
                ddsManifestation.CalmRef = work.GetIdentifierByType("calm-ref-no");
                ddsManifestation.CalmAltRef = work.GetIdentifierByType("calm-altref-no");
                if (ddsManifestation.CalmRef.HasText())
                {
                    ddsManifestation.CollectionReferenceNumber = Manifestation.EmptyTopLevelArchiveReference;
                    ddsManifestation.CollectionTitle = Manifestation.EmptyTopLevelArchiveTitle;
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
                if (work.PartOf.HasItems())
                {
                    ddsManifestation.CollectionReferenceNumber = parent.ReferenceNumber;
                    ddsManifestation.CollectionWorkId = parent.Id;
                    ddsManifestation.CollectionTitle = parent.Title;
                }

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
                        ddsManifestation.AssetType = metsManifestation.FirstInternetType;
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
                                ddsManifestation.FirstFileDuration = asset.AssetMetadata.GetDuration();
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
                ddsManifestation.DlcsAssetType = metsManifestation.FirstInternetType;
                ddsManifestation.ManifestationIdentifier = ddsId;
                ddsManifestation.VolumeIdentifier = ddsId.IdentifierType == IdentifierType.Volume
                    ? ddsId.VolumePart
                    : null;
                if (isBNumber)
                {
                    // we can only set these when processing a b number, not an individual manifestation
                    ddsManifestation.Index = mic.SequenceIndex;
                    ddsManifestation.ContainsRestrictedFiles = containsRestrictedFiles;
                }
                
                // save the ddsManifestation, which will commit the AddAsync if it was new
                // this will also commit any deletes of duplicates we have made.
                await ddsContext.SaveChangesAsync();
                
            } // end of foreach (var mic in metsRepository.GetAllManifestationsInContext(identifier)
            
            // At this point there are no uncommitted DB changes
            if (isBNumber)
            {
                string? betterTitle = null;
                if (manifestationIdsProcessed.Count == 0)
                {
                    const string message = "No manifestations for {0}, creating error manifestation";
                    const string dipStatus = "no-manifs";
                    await CreateErrorManifestation(shortB, message, identifier, dipStatus);
                }
                else
                {
                    betterTitle = work.Title;
                    await RefreshMetadata(identifier, work);
                }
                
                // Are there any manifestations in the DB still that we didn't see?
                
                // See what's already present in the manifestations table for this b number
                foreach (var ddsManifestation in ddsContext.Manifestations.Where(
                    fm => fm.PackageIdentifier == identifier))
                {
                    if (!manifestationIdsProcessed.Contains(ddsManifestation.Id))
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
                await ddsContext.SaveChangesAsync();
            }
        }

        private IPhysicalFile? GetPhysicalFileFromThumbnailPath(Work work, List<IPhysicalFile> assets)
        {
            if (work.Thumbnail == null) return null;
            var match = wcorgThumbRegex.Match(work.Thumbnail.Url);
            if (!match.Success)
            {
                match = dlcsThumbRegex.Match(work.Thumbnail.Url);
                if (!match.Success) return null;
            }
            var storageIdentifier = match.Groups[1].Value;
            return assets.FirstOrDefault(a => a.StorageIdentifier == storageIdentifier);
        }

        private string GetDlcsThumbnailServiceForAsset(IPhysicalFile asset)
        {
            return uriPatterns.DlcsThumb(dlcsOptions.ResourceEntryPoint, asset.StorageIdentifier);
        }
        
  
        private async Task CreateErrorManifestation(int shortB, string message, 
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
            await ddsContext.Manifestations.AddAsync(fm);
            await ddsContext.SaveChangesAsync();
        }
        
        
        private async Task RefreshMetadata(string identifier, Work work)
        {
            DeleteMetadata(identifier);
            await ddsContext.Metadata.AddRangeAsync(work.GetMetadata(identifier));
            await ddsContext.SaveChangesAsync();
        }

        private void DeleteMetadata(string identifier)
        {
            var fieldsForIdentifier = ddsContext.Metadata.Where(m => m.ManifestationId == identifier);
            ddsContext.Metadata.RemoveRange(fieldsForIdentifier);
        }
    }
}