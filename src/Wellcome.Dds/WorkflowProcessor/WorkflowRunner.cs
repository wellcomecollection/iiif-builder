using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using IIIF.Presentation.V3.Constants;
using Microsoft.Extensions.Caching.Memory;
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
using Wellcome.Dds.Repositories.Presentation.SpecialState;
using AccessCondition = Wellcome.Dds.Common.AccessCondition;
using Version = IIIF.Presentation.Version;

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
        private readonly DdsOptions ddsOptions;
        private readonly ICatalogue catalogue;
        private readonly BucketWriter bucketWriter;
        private readonly AltoDerivedAssetBuilder altoBuilder;
        private readonly IWorkflowJobPostProcessor postProcessor;
        private readonly IMemoryCache memoryCache;

        public WorkflowRunner(
            IIngestJobRegistry ingestJobRegistry, 
            IOptions<RunnerOptions> runnerOptions,
            ILogger<WorkflowRunner> logger,
            IDds dds,
            IIIIFBuilder iiifBuilder,
            IMetsRepository metsRepository,
            IOptions<DdsOptions> ddsOptions,
            ICatalogue catalogue,
            BucketWriter bucketWriter,
            AltoDerivedAssetBuilder altoBuilder,
            IWorkflowJobPostProcessor postProcessor,
            IMemoryCache memoryCache)
        {
            this.ingestJobRegistry = ingestJobRegistry;
            this.logger = logger;
            this.runnerOptions = runnerOptions.Value;
            this.dds = dds;
            this.iiifBuilder = iiifBuilder;
            this.metsRepository = metsRepository;
            this.ddsOptions = ddsOptions.Value;
            this.catalogue = catalogue;
            this.bucketWriter = bucketWriter;
            this.altoBuilder = altoBuilder;
            this.postProcessor = postProcessor;
            this.memoryCache = memoryCache;
        }

        public async Task ProcessJob(WorkflowJob job, CancellationToken cancellationToken = default)
        {
            job.Taken = DateTime.Now;
            var jobOptions = runnerOptions;
            if (job.WorkflowOptions != null)
            {
                // Allow an individual job to override the processor options
                jobOptions = RunnerOptions.FromInt32(job.WorkflowOptions.Value);
            }

            if (!jobOptions.HasWorkToDo())
            {
                job.Error = $"No work specified in jobOptions ({job.WorkflowOptions})";
            }
            
            Work work = null;
            bool logMissingWork = false;
            try
            {
                if (jobOptions.RegisterImages)
                {
                    var batchResponse = await ingestJobRegistry.RegisterImages(job.Identifier);
                    if (batchResponse.Length > 0)
                    {
                        job.FirstDlcsJobId = batchResponse[0].Id;
                        job.DlcsJobCount = batchResponse.Length;
                    }
                }

                if (jobOptions.RefreshFlatManifestations)
                {
                    work = await catalogue.GetWorkByOtherIdentifier(job.Identifier);
                    if (work != null)
                    {
                        await dds.RefreshManifestations(job.Identifier, work);
                    }
                    else
                    {
                        logMissingWork = true;
                    }
                }

                if (jobOptions.RebuildIIIF)
                {
                    work ??= await catalogue.GetWorkByOtherIdentifier(job.Identifier);
                    if (work != null)
                    {
                        await RebuildIIIF(job, work);
                    }
                    else
                    {
                        logMissingWork = true;
                    }
                }

                if (jobOptions.RebuildTextCaches || jobOptions.RebuildAllAnnoPageCaches)
                {
                    await altoBuilder.RebuildAltoDerivedAssets(job, jobOptions, cancellationToken);
                }

                if (logMissingWork)
                {
                    await SetJobErrorMessage(job);
                }

                await postProcessor.PostProcess(job, jobOptions);
                job.TotalTime = (long) (DateTime.Now - job.Taken.Value).TotalMilliseconds;
                logger.LogInformation("Processed {JobId} in {TotalTime}ms", job.Identifier, job.TotalTime);
            }
            catch (Exception ex)
            {
                job.Error = ex.Message.SummariseWithEllipsis(240);
                logger.LogError(ex, "Error in workflow runner");
            }
            finally
            {
                if (memoryCache is MemoryCache {Count: > 0} cache)
                {
                    logger.LogInformation("Compacting memory cache with {MemoryCount} items after processing {JobId}",
                        cache.Count,
                        job.Identifier);
                    cache.Compact(100);
                }
            }
        }

        private async Task SetJobErrorMessage(WorkflowJob job)
        {
            IManifestation manifestation = null;
            // Just look at the first one for now
            await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(job.Identifier))
            {
                if (manifestationInContext.Manifestation.Partial)
                {
                    manifestation = (IManifestation) await metsRepository.GetAsync(manifestationInContext.Manifestation.Id);
                }
                else
                {
                    manifestation = manifestationInContext.Manifestation;
                }

                break;
            }

            if (manifestation == null)
            {
                throw new NullReferenceException("Manifestation cannot be null");
            }

            var accessConditions = manifestation.Sequence.Select(pf => pf.AccessCondition);
            var highest = AccessCondition.GetMostSecureAccessCondition(accessConditions);
            if (highest == AccessCondition.Open || 
                highest == AccessCondition.RequiresRegistration ||
                highest == AccessCondition.OpenWithAdvisory)
            {
                // only set this error if we think we SHOULD be able to get a work back.
                job.Error = "No work available in Catalogue API; highest access condition is " + highest;
            }
        }

        private async Task RebuildIIIF(WorkflowJob job, Work work)
        {
            // makes new IIIF in S3 for job.Identifier (the WHOLE b number, not vols)
            // Does this from the METS and catalogue info
            var start = DateTime.Now;
            var iiif3BuildResults = await iiifBuilder.BuildAllManifestations(job.Identifier, work);
            var iiif2BuildResults = iiifBuilder.BuildLegacyManifestations(job.Identifier, iiif3BuildResults);

            // Now we save them all to S3.
            await WriteResultToS3(iiif3BuildResults);
            await WriteResultToS3(iiif2BuildResults);

            var packageEnd = DateTime.Now;
            job.PackageBuildTime = (long)(packageEnd - start).TotalMilliseconds;

            var failures = iiif2BuildResults.Concat(iiif3BuildResults)
                .Where(br => br.Outcome == BuildOutcome.Failure)
                .ToList();
            if (failures.Count > 0)
            {
                var builder = new StringBuilder(failures.Count * 2);
                foreach (var failed in failures)
                {
                    builder.Append(failed.Message);
                    builder.Append(',');
                }

                job.Error = builder.ToString();
            }
        }

        public async Task TraverseChemistAndDruggist()
        {
            var state = new ChemistAndDruggistState(null!);
            int counter = 0;
            await foreach (var mic in metsRepository.GetAllManifestationsInContext(KnownIdentifiers.ChemistAndDruggist))
            {
                logger.LogInformation($"Counter: {++counter}");
                var volume = state.Volumes.SingleOrDefault(v => v.Identifier == mic.VolumeIdentifier);
                if (volume == null)
                {
                    volume = new ChemistAndDruggistVolume(mic.VolumeIdentifier);
                    state.Volumes.Add(volume);
                    var metsVolume = await metsRepository.GetAsync(mic.VolumeIdentifier) as ICollection;
                    // populate volume fields
                    volume.Volume = metsVolume.ModsData.Number;
                    logger.LogInformation("Will parse date for VOLUME " + metsVolume.ModsData.OriginDateDisplay);
                    volume.DisplayDate = metsVolume.ModsData.OriginDateDisplay;
                    volume.NavDate = state.GetNavDate(volume.DisplayDate);
                    volume.Label = metsVolume.ModsData.Title;
                    logger.LogInformation(" ");
                    logger.LogInformation("-----VOLUME-----");
                    logger.LogInformation(volume.ToString());
                }
                logger.LogInformation($"Issue {mic.IssueIdentifier}, Volume {mic.VolumeIdentifier}");
                var issue = new ChemistAndDruggistIssue(mic.IssueIdentifier);
                volume.Issues.Add(issue);
                var metsIssue = mic.Manifestation; // is this partial?
                var mods = metsIssue.ModsData;
                // populate issue fields
                issue.Title = mods.Title; // like "2293"
                issue.DisplayDate = mods.OriginDateDisplay;
                logger.LogInformation("Will parse date for ISSUE " + issue.DisplayDate);
                issue.NavDate = state.GetNavDate(issue.DisplayDate);
                issue.Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(issue.NavDate.Month);
                issue.MonthNum = issue.NavDate.Month;
                issue.Year = issue.NavDate.Year;
                issue.Volume = volume.Volume;
                issue.PartOrder = mods.PartOrder;
                issue.Number = mods.Number;
                issue.Label = issue.DisplayDate;
                if (issue.Title.HasText() && issue.Title.Trim() != "-")
                {
                    issue.Label += $" (issue {issue.Title})";
                }
                // duplicate this info for CSV-isation
                issue.VolumeIdentifier = volume.Identifier;
                issue.VolumeLabel = volume.Label;
                issue.VolumeDisplayDate = volume.DisplayDate;
                issue.VolumeNavDate = volume.NavDate;
                logger.LogInformation(" ");
                logger.LogInformation("    -----Issue-----");
                logger.LogInformation(issue.ToString());
            }

            using (var writer = new StreamWriter("/home/tomcrane/git/wellcomecollection/iiif-builder/c-and-d.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                var issues = state.Volumes.SelectMany(vol => vol.Issues);
                csv.WriteRecords(issues);
            }
            
            var partCheck = new Dictionary<int, int>();
            
            foreach (var volume in state.Volumes)
            {
                foreach (var issue in volume.Issues)
                {
                    if (!partCheck.ContainsKey(issue.PartOrder))
                    {
                        partCheck[issue.PartOrder] = 0;
                    }

                    partCheck[issue.PartOrder] = partCheck[issue.PartOrder] + 1;
                }
            }

            foreach (var kvp in partCheck)
            {
                logger.LogInformation(kvp.Key + ": " + kvp.Value);
            }
            
            
            logger.LogInformation("########### PARTS");

            logger.LogInformation("Range of parts from " + partCheck.Keys.Count + " values");
            var min = partCheck.Select(kvp => kvp.Key).Min();
            var max = partCheck.Select(kvp => kvp.Key).Max();
            logger.LogInformation("min: " + min + ", max: " + max);
            
            logger.LogInformation("parts with more than one entry: ");
            foreach (var kvp in partCheck.Where(kvp => kvp.Value > 1))
            {
                logger.LogInformation(kvp.Key + ": " + kvp.Value);
            }
            
            logger.LogInformation("########### Dates");
        }
        
        private async Task WriteResultToS3(MultipleBuildResult buildResults)
        {
            string saveId = "-";
            try
            {
                foreach (var buildResult in buildResults.Where(r => r.Outcome == BuildOutcome.Success))
                {
                    saveId = buildResult.Id;

                    // Moving this here to add at the last minute.
                    // This allows a buildResult with references to resources in other buildResults to be serialised
                    // with the @context as long as they are serialised after the referer is serialised.
                    // The one use case for this is Chemist and Druggist, where collection b19974760 contains nested
                    // collections in the top level collection (where we don't want them to have @contexts of their own)
                    // and these nested collections are also serialised to S3 in their own right (when we DO want them
                    // to have their own @contexts).
                    if (buildResult.IIIFVersion == Version.V3)
                    {
                        buildResult.IIIFResource.EnsurePresentation3Context();
                    }
                    else if (buildResult.IIIFVersion == Version.V2)
                    {
                        buildResult.IIIFResource.EnsurePresentation2Context();
                    }

                    await bucketWriter.PutIIIFJsonObjectToS3(buildResult.IIIFResource,
                        ddsOptions.PresentationContainer, buildResult.GetStorageKey(),
                        buildResult.IIIFVersion == Version.V2 ? "IIIF 2 Resource" : "IIIF 3 Resource");
                }
            }
            catch (Exception e)
            {
                buildResults.Message = $"Failed at {saveId}, {e.Message}";
                buildResults.Outcome = BuildOutcome.Failure;
            }
        }
    }
}
