using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.IIIF;
using Wellcome.Dds.Repositories.WordsAndPictures;

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
        private readonly Synchroniser synchroniser;
        private readonly IIIIFBuilder iiifBuilder;
        private readonly IMetsRepository metsRepository;
        private readonly CachingAllAnnotationProvider cachingAllAnnotationProvider;
        private readonly CachingAltoSearchTextProvider cachingSearchTextProvider;

        public WorkflowRunner(
            IIngestJobRegistry ingestJobRegistry, 
            IOptions<RunnerOptions> runnerOptions,
            ILogger<WorkflowRunner> logger,
            Synchroniser synchroniser,
            IIIIFBuilder iiifBuilder,
            IMetsRepository metsRepository,
            CachingAllAnnotationProvider cachingAllAnnotationProvider,
            CachingAltoSearchTextProvider cachingSearchTextProvider)
        {
            this.ingestJobRegistry = ingestJobRegistry;
            this.logger = logger;
            this.runnerOptions = runnerOptions.Value;
            this.synchroniser = synchroniser;
            this.iiifBuilder = iiifBuilder;
            this.metsRepository = metsRepository;
            this.cachingAllAnnotationProvider = cachingAllAnnotationProvider;
            this.cachingSearchTextProvider = cachingSearchTextProvider;
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
                    synchroniser.RefreshFlatManifestations(job.Identifier);
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
            var result = await iiifBuilder.Build(job.Identifier);
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
    }
}