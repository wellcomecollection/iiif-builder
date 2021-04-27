using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures;

namespace WorkflowProcessor
{
    /// <summary>
    /// Builds text-based resources from alto derivatives.
    /// </summary>
    public class AltoDerivedAssetBuilder
    {
        private readonly CachingAllAnnotationProvider cachingAllAnnotationProvider;
        private readonly CachingAltoSearchTextProvider cachingSearchTextProvider;
        private readonly DdsOptions ddsOptions;
        private readonly IIIIFBuilder iiifBuilder;
        private readonly BucketWriter bucketWriter;
        private readonly IMetsRepository metsRepository;
        private readonly ILogger<AltoDerivedAssetBuilder> logger;

        public AltoDerivedAssetBuilder(
            ILogger<AltoDerivedAssetBuilder> logger,
            IMetsRepository metsRepository,
            CachingAllAnnotationProvider cachingAllAnnotationProvider,
            CachingAltoSearchTextProvider cachingSearchTextProvider,
            IOptions<DdsOptions> ddsOptions,
            IIIIFBuilder iiifBuilder, 
            BucketWriter bucketWriter)
        {
            this.logger = logger;
            this.metsRepository = metsRepository;
            this.cachingAllAnnotationProvider = cachingAllAnnotationProvider;
            this.cachingSearchTextProvider = cachingSearchTextProvider;
            this.ddsOptions = ddsOptions.Value;
            this.iiifBuilder = iiifBuilder;
            this.bucketWriter = bucketWriter;
        }
        
        public async Task RebuildAltoDerivedAssets(WorkflowJob job, RunnerOptions jobOptions, CancellationToken cancellationToken = default)
        {
            if (!jobOptions.RebuildTextCaches) return;

            var start = DateTime.Now;
            job.ExpectedTexts = 0;
            job.AnnosAlreadyOnDisk = 0;
            job.TextsAlreadyOnDisk = 0;
            job.AnnosBuilt = 0;
            job.TextsBuilt = 0;
            job.TextPages = 0;
            job.TimeSpentOnTextPages = 0;
            int wordsCountedOnThisRun = 0;
            bool wordCountInvalid = false;

            try
            {
                DeleteZipFileIfExists(job.Identifier);
                logger.LogInformation("Getting all manifestations in context for rebuilding Alto-derived assets for {identifier}", job.Identifier);
                await foreach (var manifestationInContext in metsRepository
                    .GetAllManifestationsInContext(job.Identifier)
                    .WithCancellation(cancellationToken))
                {
                    var manifestation = await GetManifestation(manifestationInContext);

                    if (manifestation != null && HasAltoFiles(manifestation))
                    {
                        job.ExpectedTexts++;
                        var textFileInfo = cachingSearchTextProvider.GetFileInfo(manifestation.Id);

                        if (await textFileInfo.DoesExist() && !job.ForceTextRebuild)
                        {
                            logger.LogInformation("Text already on disk for {ManifestationId}", manifestation.Id);
                            wordCountInvalid = true;
                            job.TextsAlreadyOnDisk++;
                        }
                        else
                        {
                            var wordCount = await RebuildText(job, manifestation);
                            wordsCountedOnThisRun += wordCount;
                        }

                        //  How do the all-annos file and the images file get built?
                        var allAnnoFileInfo = cachingAllAnnotationProvider.GetFileInfo(manifestation.Id);
                        if (await allAnnoFileInfo.DoesExist() && !job.ForceTextRebuild)
                        {
                            logger.LogInformation("All anno file already on disk for {ManifestationId}",
                                manifestation.Id);
                            job.AnnosAlreadyOnDisk++;
                        }
                        else
                        {
                            if (jobOptions.RebuildAllAnnoPageCaches)
                            {
                                // TODO: we can have an implementation of cachingAllAnnotationProvider
                                // that uses the Text object constructed in RebuildText, rather than
                                // read the ALTO files again.
                                // However - we need to consider memory use here. At this point, Text
                                // is not in scope and can be garbage collected; the cachingAllAnnotationProvider
                                // reads one ALTO file at a time to construct the annotationPages list.
                                // We would still need the full Text object in scope to build the annotation pages,
                                // which might use too much memory.
                                
                                // A complete alternative would involve doing the BuildW3CAndOaAnnotations
                                // during the Text construction, which merges together the responsibilities.
                                // This would be more efficient but is a substantial change.
                                
                                var annotationPages = await
                                    cachingAllAnnotationProvider.ForcePagesRebuild(manifestation.Id,
                                        manifestation.Sequence);
                                // Now convert them to W3C Web Annotations
                                var result = iiifBuilder.BuildW3CAndOaAnnotations(manifestation, annotationPages);
                                await SaveAnnoPagesToS3(result);
                                logger.LogInformation(
                                    "Rebuilt annotation pages for {ManifestationId}: {PagesCount} pages",
                                    manifestation.Id, annotationPages.Count);
                                job.AnnosBuilt++;
                            }
                            else
                            {
                                logger.LogInformation("Skipping AllAnnoCache rebuild for {ManifestationId}",
                                    manifestation.Id);
                            }
                        }
                    }
                }

                if (ZipFileExists(job.Identifier))
                {
                    await bucketWriter.SaveTextZipToS3(GetZipFilePath(job.Identifier), $"zip/{job.Identifier}.zip");
                }

                if (!wordCountInvalid)
                {
                    job.Words = wordsCountedOnThisRun;
                }

                var end = DateTime.Now;
                job.TextAndAnnoBuildTime = (long) (end - start).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error building alto derived assets for {Identifier}", job.Identifier);
                throw;
            }
            finally
            {
                DeleteZipFileIfExists(job.Identifier);
            }
        }

        private async Task<int> RebuildText(WorkflowJob job, IManifestation manifestation)
        {
            var startTextTs = DateTime.Now;
            var text = await cachingSearchTextProvider.ForceSearchTextRebuild(manifestation.Id);
            await bucketWriter.SaveRawTextToS3(text.RawFullText, $"raw/{manifestation.Id}");

            await AddToZip(job.Identifier, manifestation.Id, text);
            
            var wordCount = text.Words.Count;
            logger.LogInformation("Rebuilt search text for {ManifestationId}: {WordCount} words",
                manifestation.Id, wordCount);
            job.TextsBuilt++;
            job.TextPages += text.Images.Length;
            job.TimeSpentOnTextPages += (int) (DateTime.Now - startTextTs).TotalMilliseconds;
            return wordCount;
        }

        private async Task AddToZip(string identifier, string manifestId, Text text)
        {
            var zipFilePath = GetZipFilePath(identifier);
            logger.LogInformation("Adding text for {ManifestationId} to zip file {ZipFile}", manifestId, zipFilePath);
            
            var exists = File.Exists(zipFilePath);
            await using var zipToOpen = new FileStream(zipFilePath, FileMode.OpenOrCreate);
            using var zipArchive = new ZipArchive(zipToOpen, exists ? ZipArchiveMode.Update : ZipArchiveMode.Create);
            var archiveEntry = zipArchive.CreateEntry($"{manifestId}.txt");
            await using var writer = new StreamWriter(archiveEntry.Open());
            await writer.WriteAsync(text.RawFullText);
        }

        private static void DeleteZipFileIfExists(string identifier)
        {
            if (ZipFileExists(identifier))
                File.Delete(GetZipFilePath(identifier));
        }

        private static bool ZipFileExists(string identifier) => File.Exists(GetZipFilePath(identifier));
        
        private static string GetZipFilePath(string identifier) => Path.Join(Path.GetTempPath(), $"{identifier}.zip");

        private async Task<IManifestation> GetManifestation(IManifestationInContext manifestationInContext)
        {
            var manifestation = manifestationInContext.Manifestation;
            logger.LogInformation("Getting individual manifestation for Alto-derived asset building, {identifier}", manifestation.Id);
            if (manifestation.Partial)
            {
                logger.LogInformation("Manifestation is partial, going to fetch full version for {identifier}", manifestation.Id);
                manifestation = await metsRepository.GetAsync(manifestation.Id) as IManifestation;
            }

            return manifestation;
        }

        private bool HasAltoFiles(IManifestation manifestation)
        {
            return manifestation.Sequence.Any(pf => pf.RelativeAltoPath.HasText());
        }

        private async Task SaveAnnoPagesToS3(AltoAnnotationBuildResult builtAnnotations)
        {
            const string annotationsPathSegment = "/annotations/";
            // Assumption - we save each page individually to S3 (=> 20m pages...)
            // We save the allcontent single list to S3
            // and we save the image list to S3.
            if (builtAnnotations.AllContentAnnotations != null)
            {
                await bucketWriter.PutIIIFJsonObjectToS3(
                    builtAnnotations.AllContentAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.AllContentAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "W3C whole manifest annotations");
            }
            
            if (builtAnnotations.ImageAnnotations != null)
            {
                await bucketWriter.PutIIIFJsonObjectToS3(
                    builtAnnotations.ImageAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.ImageAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "W3C manifest image/figure/block annotations");
            }

            if (builtAnnotations.PageAnnotations != null)
            {
                for (int i = 0; i < builtAnnotations.PageAnnotations.Length; i++)
                {
                    await bucketWriter.PutIIIFJsonObjectToS3(
                        builtAnnotations.PageAnnotations[i],
                        ddsOptions.AnnotationContainer,
                        builtAnnotations.PageAnnotations[i].Id.Split(annotationsPathSegment)[^1],
                        "W3C page annotations");
                }
            }
            
            // For the Open Annotation versions, we just save the manifest-level all- and image- lists.
            // We won't save the page level versions (we didn't make them!)
            if (builtAnnotations.OpenAnnotationAllContentAnnotations != null)
            {
                await bucketWriter.PutIIIFJsonObjectToS3(
                    builtAnnotations.OpenAnnotationAllContentAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.OpenAnnotationAllContentAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "OA whole manifest annotations");
            }
            
            if (builtAnnotations.ImageAnnotations != null)
            {
                await bucketWriter.PutIIIFJsonObjectToS3(
                    builtAnnotations.OpenAnnotationImageAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.OpenAnnotationImageAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "OA manifest image/figure/block annotations");
            }
        }
    }
}