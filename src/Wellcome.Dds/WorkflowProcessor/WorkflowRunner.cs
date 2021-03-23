using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using CsvHelper;
using IIIF;
using IIIF.Presentation.V3.Constants;
using IIIF.Serialisation;
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
using Wellcome.Dds.Repositories.WordsAndPictures;
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
                    await RebuildAltoDerivedAssets(job, jobOptions); 
                }

                if (logMissingWork)
                {
                    await SetJobErrorMessage(job);
                }

                job.TotalTime = (long)(DateTime.Now - job.Taken.Value).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                job.Error = ex.Message.SummariseWithEllipsis(240);
                logger.LogError(ex, "Error in workflow runner");
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
            job.Error = "No work available in Catalogue API; highest access condition is " + highest;
        }

        private async Task RebuildIIIF(WorkflowJob job, Work work)
        {
            // makes new IIIF in S3 for job.Identifier (the WHOLE b number, not vols)
            // Does this from the METS and catalogue info
            var start = DateTime.Now;
            var iiif3BuildResults = await iiifBuilder.BuildAllManifestations(job.Identifier, work);
            var iiif2BuildResults = iiifBuilder.BuildLegacyManifestations(job.Identifier, iiif3BuildResults);
            
            // Now we save them all to S3.
            string saveId = "-";
            try
            {
                foreach (var buildResult in iiif3BuildResults.Concat(iiif2BuildResults))
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
                    else if(buildResult.IIIFVersion == Version.V2)
                    {
                        buildResult.IIIFResource.EnsurePresentation2Context();
                    }
                    await PutIIIFJsonObjectToS3(buildResult.IIIFResource,
                        ddsOptions.PresentationContainer, buildResult.GetStorageKey(),
                        buildResult.IIIFVersion == Version.V2 ? "IIIF 2 Resource" : "IIIF 3 Resource");
                }
            }
            catch (Exception e)
            {
                iiif3BuildResults.Message = $"Failed at {saveId}, {e.Message}";
                iiif3BuildResults.Outcome = BuildOutcome.Failure;
            }

            var packageEnd = DateTime.Now;
            job.PackageBuildTime = (long)(packageEnd - start).TotalMilliseconds;
            if (iiif3BuildResults.Any(br => br.Outcome != BuildOutcome.Success))
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

        private async Task RebuildAltoDerivedAssets(WorkflowJob job, RunnerOptions jobOptions)
        {
            if (jobOptions.RebuildTextCaches)
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
                            if (jobOptions.RebuildAllAnnoPageCaches)
                            {
                                // These are in our internal text model
                                var annotationPages = await 
                                    cachingAllAnnotationProvider.ForcePagesRebuild(manifestation.Id, manifestation.Sequence);
                                // Now convert them to W3C Web Annotations
                                var result = iiifBuilder.BuildW3CAndOaAnnotations(manifestation, annotationPages);
                                await SaveAnnoPagesToS3(result);
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

        private async Task PutIIIFJsonObjectToS3(JsonLdBase iiifResource, string bucket, string key, string logLabel)
        {
            var put = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = iiifResource.AsJson(),
                ContentType = "application/json"
            };
            logger.LogInformation("Putting {LogLabel} to S3: bucket: {BucketName}, key: {Key}", logLabel,
                put.BucketName, put.Key);
            await amazonS3.PutObjectAsync(put);
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
            logger.LogInformation($"Putting raw text to S3: bucket: {put.BucketName}, key: {put.Key}");
            await amazonS3.PutObjectAsync(put);
        }

        private async Task SaveAnnoPagesToS3(AltoAnnotationBuildResult builtAnnotations)
        {
            const string annotationsPathSegment = "/annotations/";
            // Assumption - we save each page individually to S3 (=> 20m pages...)
            // We save the allcontent single list to S3
            // and we save the image list to S3.
            if (builtAnnotations.AllContentAnnotations != null)
            {
                await PutIIIFJsonObjectToS3(
                    builtAnnotations.AllContentAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.AllContentAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "W3C whole manifest annotations");
            }
            
            if (builtAnnotations.ImageAnnotations != null)
            {
                await PutIIIFJsonObjectToS3(
                    builtAnnotations.ImageAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.ImageAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "W3C manifest image/figure/block annotations");
            }

            if (builtAnnotations.PageAnnotations != null)
            {
                for (int i = 0; i < builtAnnotations.PageAnnotations.Length; i++)
                {
                    await PutIIIFJsonObjectToS3(
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
                await PutIIIFJsonObjectToS3(
                    builtAnnotations.OpenAnnotationAllContentAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.OpenAnnotationAllContentAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "OA whole manifest annotations");
            }
            
            if (builtAnnotations.ImageAnnotations != null)
            {
                await PutIIIFJsonObjectToS3(
                    builtAnnotations.OpenAnnotationImageAnnotations,
                    ddsOptions.AnnotationContainer,
                    builtAnnotations.OpenAnnotationImageAnnotations.Id.Split(annotationsPathSegment)[^1],
                    "OA manifest image/figure/block annotations");
            }
        }

        public async Task TraverseChemistAndDruggist()
        {
            var state = new ChemistAndDruggistState(null!);
            int counter = 0;
            await foreach (var mic in metsRepository.GetAllManifestationsInContext("b19974760"))
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

        
    }
}
