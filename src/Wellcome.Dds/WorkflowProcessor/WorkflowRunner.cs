using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using IIIF.Presentation;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures.SimpleAltoServices;

namespace WorkflowProcessor
{
    /// <summary>
    /// Main task runner for WorkflowProcessor
    /// </summary>
    public class WorkflowRunner
    {
        private readonly IIngestJobRegistry ingestJobRegistry;
        private readonly ILogger<WorkflowRunner> logger;
        private readonly RunnerOptions runnerOptions;
        private readonly IDds dds;
        private readonly IIIIFBuilder iiifBuilder;
        private readonly IMetsRepository metsRepository;
        private readonly CachingAllAnnotationProvider cachingAllAnnotationProvider;
        private readonly CachingAltoSearchTextProvider cachingSearchTextProvider;
        private readonly DdsOptions ddsOptions;
        private readonly ICatalogue catalogue;
        private readonly IAmazonS3 amazonS3;

        public WorkflowRunner(
            IIngestJobRegistry ingestJobRegistry, 
            IOptions<RunnerOptions> runnerOptions,
            ILogger<WorkflowRunner> logger,
            IDds dds,
            IIIIFBuilder iiifBuilder,
            IMetsRepository metsRepository,
            CachingAllAnnotationProvider cachingAllAnnotationProvider,
            CachingAltoSearchTextProvider cachingSearchTextProvider,
            IOptions<DdsOptions> ddsOptions,
            ICatalogue catalogue,
            IAmazonS3 amazonS3)
        {
            this.ingestJobRegistry = ingestJobRegistry;
            this.logger = logger;
            this.runnerOptions = runnerOptions.Value;
            this.dds = dds;
            this.iiifBuilder = iiifBuilder;
            this.metsRepository = metsRepository;
            this.cachingAllAnnotationProvider = cachingAllAnnotationProvider;
            this.cachingSearchTextProvider = cachingSearchTextProvider;
            this.ddsOptions = ddsOptions.Value;
            this.catalogue = catalogue;
            this.amazonS3 = amazonS3;
        }

        public async Task ProcessJob(WorkflowJob job, CancellationToken cancellationToken = default)
        {
            job.Taken = DateTime.Now;
            Work work = null;
            try
            {
                if (runnerOptions.RegisterImages)
                {
                    var batchResponse = await ingestJobRegistry.RegisterImages(job.Identifier);
                    if (batchResponse.Length > 0)
                    {
                        job.FirstDlcsJobId = batchResponse[0].Id;
                        job.DlcsJobCount = batchResponse.Length;
                    }
                }
                if (runnerOptions.RefreshFlatManifestations)
                {
                    work = await catalogue.GetWorkByOtherIdentifier(job.Identifier);
                    await dds.RefreshManifestations(job.Identifier, work);
                }
                if (runnerOptions.RebuildIIIF3)
                {
                    work ??= await catalogue.GetWorkByOtherIdentifier(job.Identifier);
                    await RebuildIIIF3(job, work);
                }
                if (runnerOptions.RebuildTextCaches || runnerOptions.RebuildAllAnnoPageCaches)
                {
                    await RebuildAltoDerivedAssets(job); 
                }

                job.TotalTime = (long)(DateTime.Now - job.Taken.Value).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                job.Error = ex.Message.SummariseWithEllipsis(240);
                logger.LogError(ex, "Error in workflow runner");
            }
        }

        private async Task RebuildIIIF3(WorkflowJob job, Work work)
        {
            // makes new IIIF in S3 for job.Identifier (the WHOLE b number, not vols)
            // Does this from the METS and catalogue info
            var start = DateTime.Now;
            var buildResults = await iiifBuilder.BuildAllManifestations(job.Identifier, work);
            
            // Now we save them all to S3.
            string saveId = "-";
            try
            {
                foreach (var buildResult in buildResults)
                {
                    saveId = buildResult.Id;
                    await Save(buildResult);
                }
            }
            catch (Exception e)
            {
                buildResults.Message = $"Failed at {saveId}, {e.Message}";
                buildResults.Outcome = BuildOutcome.Failure;
            }

            var packageEnd = DateTime.Now;
            job.PackageBuildTime = (long)(packageEnd - start).TotalMilliseconds;
            if(buildResults.Any(br => br.Outcome != BuildOutcome.Success))
            {
                // TODO
                // if(result.Outcome == BuildOutcome.HasClosedSection)
                // {
                //     return;
                // }
                // if(result.Outcome == BuildOutcome.MissingDzLicenseCode)
                // {
                //     throw new NotSupportedException("MODS Section does not contain a DZ License Code");
                // }
            }
        }
        
        
        private async Task Save(BuildResult buildResult)
        {
            await SaveToS3(buildResult.IIIF3Resource, buildResult.IIIF3Key);
            await SaveToS3(buildResult.IIIF2Resource, buildResult.IIIF2Key);
        }

        private async Task RebuildAltoDerivedAssets(WorkflowJob job)
        {
            if (runnerOptions.RebuildTextCaches)
            {
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

                // TODO - this needs a load of error handling etc
                await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(job.Identifier))
                {
                    var manifestation = manifestationInContext.Manifestation;
                    if(manifestation.Partial)
                    {
                        manifestation = await metsRepository.GetAsync(manifestation.Id) as IManifestation;
                    }
                    if (manifestation != null && HasAltoFiles(manifestation))
                    {
                        job.ExpectedTexts++;
                        var textFileInfo = cachingSearchTextProvider.GetFileInfo(manifestation.Id);
                        if (textFileInfo.Exists && !job.ForceTextRebuild)
                        {
                            logger.LogInformation($"Text already on disk for {manifestation.Id}");
                            wordCountInvalid = true;
                            job.TextsAlreadyOnDisk++;
                        }
                        else
                        {
                            var startTextTs = DateTime.Now;
                            var text = await cachingSearchTextProvider.ForceSearchTextRebuild(manifestation.Id);
                            await SaveRawTextToS3(text.RawFullText, $"raw/{manifestation.Id}");
                            var wordCount = text.Words.Count;
                            logger.LogInformation($"Rebuilt search text for {manifestation.Id}: {wordCount} words.");
                            job.TextsBuilt++;
                            wordsCountedOnThisRun += wordCount;
                            job.TextPages += text.Images.Length;
                            job.TimeSpentOnTextPages += (int)(DateTime.Now - startTextTs).TotalMilliseconds;
                        }
                        
                        //  How do the all-annos file and the images file get built?
                        var allAnnoFileInfo = cachingAllAnnotationProvider.GetFileInfo(manifestation.Id);
                        if (allAnnoFileInfo.Exists && !job.ForceTextRebuild)
                        {
                            logger.LogInformation($"All anno file already on disk for {manifestation.Id}");
                            job.AnnosAlreadyOnDisk++;
                        }
                        else
                        {
                            if (runnerOptions.RebuildAllAnnoPageCaches)
                            {
                                // These are in our internal text model
                                var annotationPages = await 
                                    cachingAllAnnotationProvider.ForcePagesRebuild(manifestation.Id, manifestation.Sequence);
                                // Now convert them to W3C Web Annotations
                                var result = iiifBuilder.BuildW3CAnnotations(manifestation, annotationPages);
                                SaveAnnoPagesToS3(result);
                                logger.LogInformation(
                                    $"Rebuilt annotation pages for {manifestation.Id}: {annotationPages.Count} pages.");
                                job.AnnosBuilt++;
                            }
                            else
                            {
                                logger.LogInformation($"Skipping AllAnnoCache rebuild for {manifestation.Id}");
                            }
                        }
                    }
                }
                if (!wordCountInvalid)
                {
                    job.Words = wordsCountedOnThisRun;
                }
                var end = DateTime.Now;
                job.TextAndAnnoBuildTime = (long)(end - start).TotalMilliseconds;
            }
        }

        private bool HasAltoFiles(IManifestation manifestation)
        {
            return manifestation.Sequence.Any(pf => pf.RelativeAltoPath.HasText());
        }

        private async Task SaveToS3(StructureBase iiifResource, string key)
        {
            var put = new PutObjectRequest
            {
                BucketName = ddsOptions.PresentationContainer,
                Key = key,
                ContentBody = iiifBuilder.Serialise(iiifResource),
                ContentType = "application/json"
            };
            LogPutObject("IIIF Resource", put);
            await amazonS3.PutObjectAsync(put);
        }

        private void LogPutObject(string label, PutObjectRequest put)
        {
            logger.LogInformation($"Putting {label} to S3: bucket: {put.BucketName}, key: {put.Key}");
        }


        private async Task SaveRawTextToS3(string content, string key)
        {
            if(content.IsNullOrWhiteSpace())
            {
                return;
            }
            var put = new PutObjectRequest
            {
                BucketName = ddsOptions.TextContainer,
                Key = key,
                ContentBody = content,
                ContentType = "text/plain"
            };
            LogPutObject("raw text", put);
            await amazonS3.PutObjectAsync(put);
        }

        private async void SaveAnnoPagesToS3(AltoAnnotationBuildResult builtAnnotations)
        {
            // Assumption - we save each page individually to S3 (=> 20m pages...)
            // We save the allcontent single list to S3
            // and we save the image list to S3.
            if (builtAnnotations.AllContentAnnotations != null)
            {
                var put = new PutObjectRequest
                {
                    BucketName = ddsOptions.AnnotationContainer,
                    Key = builtAnnotations.AllContentAnnotationsKey,
                    ContentBody = iiifBuilder.Serialise(builtAnnotations.AllContentAnnotations),
                    ContentType = "application/json"
                };
                LogPutObject("whole manifest annotations", put);
                await amazonS3.PutObjectAsync(put);
            }
            
            if (builtAnnotations.ImageAnnotations != null)
            {
                var put = new PutObjectRequest
                {
                    BucketName = ddsOptions.AnnotationContainer,
                    Key = builtAnnotations.ImageAnnotationsKey,
                    ContentBody = iiifBuilder.Serialise(builtAnnotations.ImageAnnotations),
                    ContentType = "application/json"
                };
                LogPutObject("manifest image annotations", put);
                await amazonS3.PutObjectAsync(put);
            }

            if (builtAnnotations.PageAnnotations != null)
            {
                for (int i = 0; i < builtAnnotations.PageAnnotations.Length; i++)
                {
                    var put = new PutObjectRequest
                    {
                        BucketName = ddsOptions.AnnotationContainer,
                        Key = builtAnnotations.PageAnnotationsKeys[i],
                        ContentBody = iiifBuilder.Serialise(builtAnnotations.PageAnnotations[i]),
                        ContentType = "application/json"
                    };
                    LogPutObject("page annotations", put);
                    await amazonS3.PutObjectAsync(put);
                }
            }
        }
    }
}