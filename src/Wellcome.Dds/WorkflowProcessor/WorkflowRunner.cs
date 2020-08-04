using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.IIIF;

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

        public WorkflowRunner(
            IIngestJobRegistry ingestJobRegistry, 
            IOptions<RunnerOptions> runnerOptions,
            ILogger<WorkflowRunner> logger,
            Synchroniser synchroniser,
            IIIIFBuilder iiifBuilder)
        {
            this.ingestJobRegistry = ingestJobRegistry;
            this.logger = logger;
            this.runnerOptions = runnerOptions.Value;
            this.synchroniser = synchroniser;
            this.iiifBuilder = iiifBuilder;
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
                    RebuildIIIF3Async(job);
                }
                if (runnerOptions.RebuildTextCaches || runnerOptions.RebuildAllAnnoPageCaches)
                {
                    RebuildDiskCaches(job); // this does 
                }

                job.TotalTime = (long)(DateTime.Now - job.Taken.Value).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                job.Error = ex.Message.SummariseWithEllipsis(240);
                logger.LogError(ex, "Error in workflow runner");
            }
        }

        private async Task RebuildIIIF3Async(WorkflowJob job)
        {
            // makes new IIIF in S3 for job.Identifier (the WHOLE b number, not vols)
            // Does this from the METS and catalogue info
            var start = DateTime.Now;
            var packageEnd = DateTime.Now;
            var result = await iiifBuilder.Build(job.Identifier);
            packageEnd = DateTime.Now;
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

        private void RebuildDiskCaches(WorkflowJob job)
        {
            // (transported comment - from old DDS equivalent)
            // NEED C&D protection in here
            // cachebuster here? With delay? Just go for it?
            // There is no debounce going on here.
            // This could potentially get called while a previous one is being built.
            var start = DateTime.Now;
            WdlPackage package;
            var packageEnd = DateTime.Now;
            if (RunnerConfig.RebuildPackageCaches)
            {
                package = (WdlPackage)cachingPackageProvider.ForcePackageRebuild(job.Identifier);
                packageEnd = DateTime.Now;
                job.PackageBuildTime = (long)(packageEnd - start).TotalMilliseconds;
            }
            else
            {
                package = (WdlPackage)cachingPackageProvider.GetPackage(
                    job.Identifier, ManifestationReferenceBehaviour.AllButFirstAreReferences);
            }
            if (package.Status.HasText())
            {
                const string dzError = "MODS Section does not contain a DZ License Code";
                if (package.Status.Contains(AccessCondition.ClosedSectionError))
                {
                    return;
                }
                if (package.Status.Contains(dzError))
                {
                    throw new NotSupportedException(dzError);
                }
            }
            if (RunnerConfig.RebuildTextCaches)
            {
                job.ExpectedTexts = 0;
                job.AnnosAlreadyOnDisk = 0;
                job.TextsAlreadyOnDisk = 0;
                job.AnnosBuilt = 0;
                job.TextsBuilt = 0;
                job.TextPages = 0;
                job.TimeSpentOnTextPages = 0;
                int wordsCountedOnThisRun = 0;
                bool wordCountInvalid = false;
                for (int manifestationIndex = 0;
                    manifestationIndex < package.AssetSequences.Length;
                    manifestationIndex++)
                {
                    var assetSequence = package.AssetSequences[manifestationIndex];
                    if (assetSequence.IsUri())
                    {
                        assetSequence = cachingPackageProvider.GetAssetSequence(job.Identifier, manifestationIndex);
                    }
                    var wdlAssetSequence = assetSequence as WdlAssetSequence;
                    if (wdlAssetSequence != null && wdlAssetSequence.SupportsSearch)
                    {
                        job.ExpectedTexts++;
                        var textFileInfo = cachingSearchTextProvider.GetFileInfo(job.Identifier, manifestationIndex);
                        if (textFileInfo.Exists && !job.ForceTextRebuild)
                        {
                            Log.Info("Text already on disk for " + job.Identifier + "/" + manifestationIndex);
                            wordCountInvalid = true;
                            job.TextsAlreadyOnDisk++;
                        }
                        else
                        {
                            var startTextTs = DateTime.Now;
                            var text = cachingSearchTextProvider.ForceSearchTextRebuild(job.Identifier,
                                manifestationIndex);
                            var wordCount = text.Words.Count;
                            Log.InfoFormat("Rebuilt search text for {0}/{1}: {2} words.",
                                job.Identifier, manifestationIndex, wordCount);
                            job.TextsBuilt++;
                            wordsCountedOnThisRun += wordCount;
                            job.TextPages += text.Images.Length;
                            job.TimeSpentOnTextPages += (int)(DateTime.Now - startTextTs).TotalMilliseconds;
                        }
                        var allAnnoFileInfo = cachingAllAnnotationProvider.GetFileInfo(job.Identifier,
                            manifestationIndex);
                        if (allAnnoFileInfo.Exists && !job.ForceTextRebuild)
                        {
                            Log.Info("All anno file already on disk for " + job.Identifier + "/" + manifestationIndex);
                            job.AnnosAlreadyOnDisk++;
                        }
                        else
                        {
                            if (RunnerConfig.RebuildAllAnnoPageCaches)
                            {
                                var dziImages = wdlAssetSequence.Assets.OfType<WdlSeadragonDeepZoomImage>();
                                var annopages = cachingAllAnnotationProvider.ForcePagesRebuild(job.Identifier,
                                    manifestationIndex,
                                    dziImages);
                                Log.InfoFormat("Rebuilt annotation pages for {0}/{1}: {2} pages.",
                                    job.Identifier, manifestationIndex, annopages.Count);
                                job.AnnosBuilt++;
                            }
                            else
                            {
                                Log.Info("Skipping AllAnnoCache rebuild for " + job.Identifier + "/" + manifestationIndex);
                            }
                        }
                    }
                }
                if (!wordCountInvalid)
                {
                    job.Words = wordsCountedOnThisRun;
                }
                var end = DateTime.Now;
                job.TextAndAnnoBuildTime = (long)(end - packageEnd).TotalMilliseconds;
            }
        }

    }
}