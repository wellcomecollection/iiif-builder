using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
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
            this.amazonS3 = amazonS3;
        }

        public async Task ProcessJob(WorkflowJob job, CancellationToken cancellationToken = default)
        {
            job.Taken = DateTime.Now;

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
                    await dds.RefreshManifestations(job.Identifier);
                }
                if (runnerOptions.RebuildIIIF3)
                {
                    await RebuildIIIF3(job);
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

        private async Task RebuildIIIF3(WorkflowJob job)
        {
            // makes new IIIF in S3 for job.Identifier (the WHOLE b number, not vols)
            // Does this from the METS and catalogue info
            var start = DateTime.Now;
            var result = await iiifBuilder.BuildAllManifestations(job.Identifier);
            var packageEnd = DateTime.Now;
            job.PackageBuildTime = (long)(packageEnd - start).TotalMilliseconds;
            if(result.Outcome != BuildOutcome.Success)
            {
                if(result.Outcome == BuildOutcome.HasClosedSection)
                {
                    return;
                }
                if(result.Outcome == BuildOutcome.MissingDzLicenseCode)
                {
                    throw new NotSupportedException("MODS Section does not contain a DZ License Code");
                }
            }
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
                                var annopages = await cachingAllAnnotationProvider.ForcePagesRebuild(manifestation.Id, manifestation.SignificantSequence);
                                SaveAnnoPagesToS3(manifestation, annopages);
                                logger.LogInformation($"Rebuilt annotation pages for {manifestation.Id}: {annopages.Count} pages.");
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
            return manifestation.SignificantSequence.Any(pf => pf.RelativeAltoPath.HasText());
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
            await amazonS3.PutObjectAsync(put);
        }

        private void SaveAnnoPagesToS3(IManifestation manifestation, AnnotationPageList annopages)
        {
            // TODO - this should be implemented once we have a W3C version of annopage => annos
            // so we can save it as JSON.
        }
    }
}