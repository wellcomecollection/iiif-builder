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
            this.dlcsOptions = dlcsOptions.Value;
            if (this.dlcsOptions.ResourceEntryPoint.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("DLCS Resource Entry Point not specified in options");
            }
            this.metsRepository = metsRepository;
            this.logger = logger;
            this.ddsContext = ddsContext;
            this.catalogue = catalogue;
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
        public async Task RefreshDdsManifestations(DdsIdentifier identifier, Work? work = null)
        {
            logger.LogInformation("Synchronising {id}", identifier);
            // var shortB = -1;
            var manifestationIdsProcessed = new List<string>();
            var containsRestrictedFiles = false;
            IMetsResource? packageMetsResource = null;
            IFileBasedResource? packageFileResource = null;
            work ??= await catalogue.GetWorkByOtherIdentifier(identifier.PackageIdentifier);
            if (work == null)
            {
                throw new ArgumentException($"Work in Synchroniser cannot be null - {identifier.PackageIdentifier}", nameof(work));
            }
            if (identifier.IsPackageLevelIdentifier)
            {
                // operations we can only do when the identifier being processed is a b number
                
                // remove any error manifestations, we can recreate them
                var errors = ddsContext.Manifestations
                    .Where(fm => fm.PackageIdentifier == identifier.ToString() && fm.Index < 0);
                foreach (var error in errors)
                {
                    ddsContext.Manifestations.Remove(error);
                }
                logger.LogInformation("About to save changes after removing error manifestations");
                await ddsContext.SaveChangesAsync();

                // WHAT TO DO... leave shortB as -1?
                // shortB = identifier.HasBNumber ? identifier.BNumber.ToShortBNumber() : 0; 
                logger.LogInformation("Getting METS resource for synchroniser: {identifier}", identifier);
                packageMetsResource = await metsRepository.GetAsync(identifier);
                packageFileResource = packageMetsResource;
            }

            // At this point no uncommitted DB state
            
            // Get all the manifestations for this identifier. If the identifier is a b number, it will be
            // all possible manifestations currently defined in METS for this b number.
            await foreach (var mic in metsRepository.GetAllManifestationsInContext(identifier))
            {
                IManifestation metsManifestation = mic.Manifestation;
                if (metsManifestation.Partial)
                {
                    logger.LogInformation("Getting individual manifestation for synchroniser: {identifier}", metsManifestation.Identifier);
                    metsManifestation = (IManifestation) (await metsRepository.GetAsync(metsManifestation.Identifier!))!;
                }
                if (identifier.IsPackageLevelIdentifier)
                {
                    if (metsManifestation.SectionMetadata!.AccessCondition == "Restricted files")
                    {
                        containsRestrictedFiles = true;
                    }
                    manifestationIdsProcessed.Add(metsManifestation.Identifier!);
                }

                var ddsManifestation = await ddsContext.Manifestations.FindAsync(metsManifestation.Identifier!.ToString());
                var assets = metsManifestation.Sequence ?? throw new InvalidOperationException("Manifestation has no Sequence");
                
                // (some Change code removed here) - we're not going to implement this for now
                if (ddsManifestation == null)
                {
                    logger.LogInformation("No existing record for {identifier}", metsManifestation.Identifier);
                    ddsManifestation = new Manifestation
                    {
                        Id = metsManifestation.Identifier,
                        PackageIdentifier = metsManifestation.Identifier.PackageIdentifier,
                        PackageShortBNumber = metsManifestation.Identifier.PackageIdentifier.ToShortBNumber(),
                        Index = mic.SequenceIndex
                    };
                    if (packageMetsResource != null)
                    {
                        ddsManifestation.PackageLabel = packageMetsResource.Label;
                    }
                    ddsManifestation.RootSectionType = metsManifestation.Type;
                    
                    // The instance of entity type 'Manifestation' cannot be tracked because another
                    // instance with the same key value for {'Id'} is already being tracked. 
                    LogDates("About to Add new manifestation", ddsManifestation);
                    await ddsContext.Manifestations.AddAsync(ddsManifestation);
                }

                ddsManifestation.Label =
                    metsManifestation.Label.HasText() ? metsManifestation.Label : "(no label in METS)";
                ddsManifestation.WorkId = work.Id;
                ddsManifestation.WorkType = work.WorkType!.Id;
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
                ddsManifestation.RootSectionAccessCondition = metsManifestation.SectionMetadata!.AccessCondition;
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
                            asset.AssetMetadata!.GetFileName()!.GetFileExtension().ToLowerInvariant();
                        ddsManifestation.DipStatus = null;
                        switch (metsManifestation.Sequence.First().GetDefaultProcessingBehaviour().AssetFamily)
                        {
                            case AssetFamily.Image:
                                ddsManifestation.FirstFileThumbnailDimensions = asset.GetAvailableSizeAsString();
                                ddsManifestation.FirstFileThumbnail = GetDlcsThumbnailServiceForAsset(asset);
                                if (ddsManifestation.Index == 0)
                                {
                                    // the first manifestation; add in the thumb from the catalogue, too
                                    IPhysicalFile? catThumbAsset = GetPhysicalFileFromThumbnailPath(work, assets);
                                    if (catThumbAsset != null)
                                    {
                                        ddsManifestation.CatalogueThumbnailDimensions = catThumbAsset.GetAvailableSizeAsString();
                                        ddsManifestation.CatalogueThumbnail = GetDlcsThumbnailServiceForAsset(catThumbAsset);
                                    }
                                }
                                break;
                            case AssetFamily.TimeBased:
                                ddsManifestation.FirstFileThumbnailDimensions =
                                    asset.AssetMetadata.GetDisplayDuration();
                                ddsManifestation.FirstFileDuration = asset.AssetMetadata.GetDuration();
                                break;
                        }
                    }
                }
                if (packageFileResource != null)
                {
                    ddsManifestation.PackageFile = packageFileResource.SourceFile!.Uri;
                    logger.LogInformation("Setting PackageFileModified to {lastWriteTime}", packageFileResource.SourceFile!.LastWriteTime);
                    logger.LogInformation("This date is a {kind}", packageFileResource.SourceFile.LastWriteTime.Kind);
                    ddsManifestation.PackageFileModified = packageFileResource.SourceFile.LastWriteTime;
                }
                var fsr = (IFileBasedResource) metsManifestation;
                ddsManifestation.ManifestationFile = fsr.SourceFile!.Uri;
                logger.LogInformation("Setting ManifestationFileModified to {lastWriteTime}", fsr.SourceFile!.LastWriteTime);
                logger.LogInformation("This date is a {kind}", fsr.SourceFile.LastWriteTime.Kind);
                ddsManifestation.ManifestationFileModified = fsr.SourceFile.LastWriteTime;
                ddsManifestation.Processed = DateTime.UtcNow;

                // extra fields that only the new dash knows about
                ddsManifestation.DlcsAssetType = metsManifestation.FirstInternetType;
                ddsManifestation.ManifestationIdentifier = metsManifestation.Identifier;
                ddsManifestation.VolumeIdentifier = metsManifestation.Identifier.IdentifierType == IdentifierType.Volume
                    ? metsManifestation.Identifier.VolumePart
                    : null;
                if (identifier.IsPackageLevelIdentifier)
                {
                    // we can only set these when processing a b number, not an individual manifestation
                    ddsManifestation.Index = mic.SequenceIndex;
                    ddsManifestation.ContainsRestrictedFiles = containsRestrictedFiles;
                }
                
                // save the ddsManifestation, which will commit the AddAsync if it was new
                // this will also commit any deletes of duplicates we have made.
                LogDates("About to SaveChanges on edited Manifestation", ddsManifestation);
                await ddsContext.SaveChangesAsync();
                
            } // end of foreach (var mic in metsRepository.GetAllManifestationsInContext(identifier)
            
            // At this point there are no uncommitted DB changes
            if (identifier.IsPackageLevelIdentifier)
            {
                string? betterTitle = null;
                if (manifestationIdsProcessed.Count == 0)
                {
                    const string message = "No manifestations for {0}, creating error manifestation";
                    const string dipStatus = "no-manifs";
                    await CreateErrorManifestation(identifier, message, dipStatus);
                }
                else
                {
                    betterTitle = work.Title;
                    await RefreshMetadata(identifier, work);
                }
                
                // Are there any manifestations in the DB still that we didn't see?
                
                // See what's already present in the manifestations table for this b number
                foreach (var ddsManifestation in ddsContext.Manifestations.Where(
                    fm => fm.PackageIdentifier == identifier.ToString()))
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
            var match = wcorgThumbRegex.Match(work.Thumbnail.Url!);
            if (!match.Success)
            {
                match = dlcsThumbRegex.Match(work.Thumbnail.Url!);
                if (!match.Success) return null;
            }
            var storageIdentifier = match.Groups[1].Value;
            return assets.FirstOrDefault(a => a.StorageIdentifier == storageIdentifier);
        }

        private string GetDlcsThumbnailServiceForAsset(IPhysicalFile asset)
        {
            return uriPatterns.DlcsThumb(dlcsOptions.ResourceEntryPoint!, asset.StorageIdentifier);
        }
        
  
        private async Task CreateErrorManifestation(DdsIdentifier identifier, string message, string dipStatus)
        {
            DeleteMetadata(identifier);
            logger.LogWarning(message, identifier);
            var fm = new Manifestation
            {
                Id = identifier,
                PackageIdentifier = identifier.PackageIdentifier,
                PackageShortBNumber = identifier.PackageIdentifier.ToShortBNumber(),
                Index = -1,
                Processed = DateTime.UtcNow,
                DipStatus = dipStatus
            };
            await ddsContext.Manifestations.AddAsync(fm);
            LogDates("CreateErrorManifestation", fm);
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

        private void LogDates(string context, Manifestation manifestation)
        {
            logger.LogInformation("#~#~# Manifestation Dates for {context}", context);
            logger.LogInformation("Processed: {processed}", manifestation.Processed);
            logger.LogInformation("Processed Kind: {kind}", manifestation.Processed.Kind);
            
            logger.LogInformation("PackageFileModified: {packageFileModified}", manifestation.PackageFileModified);
            logger.LogInformation("PackageFileModified Kind: {kind}", manifestation.PackageFileModified?.Kind);
            
            logger.LogInformation("ManifestationFileModified: {manifestationFileModified}", manifestation.ManifestationFileModified);
            logger.LogInformation("ManifestationFileModified Kind: {kind}", manifestation.ManifestationFileModified?.Kind);
        }
    }
}