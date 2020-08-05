using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils;
using Utils.Caching;
using Utils.Logging;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class DashController : Controller
    {
        private readonly IDashboardRepository dashboardRepository;
        private readonly IIngestJobRegistry jobRegistry;
        private readonly IIngestJobProcessor jobProcessor;
        private readonly ISimpleCache cache;
        private readonly IStatusProvider statusProvider;
        private readonly IDatedIdentifierProvider recentlyDigitisedIdentifierProvider;
        private readonly IWorkStorageFactory workStorageFactory;
        private readonly CacheBuster cacheBuster;
        private readonly DdsOptions ddsOptions;
        private readonly DlcsOptions dlcsOptions;
        private readonly IDds dds;
        private readonly IWebHostEnvironment webHostEnvironment;

        const string CacheKeyPrefix = "dashcontroller_";
        const int CacheSeconds = 5;
        private Synchroniser synchroniser;

        public DashController(
            IDashboardRepository dashboardRepository,
            ISimpleCache cache,
            IIngestJobRegistry jobRegistry,
            IIngestJobProcessor jobProcessor,
            IStatusProvider statusProvider,
            Synchroniser synchroniser,
            IDatedIdentifierProvider recentlyDigitisedIdentifierProvider,
            IWorkStorageFactory workStorageFactory,
            CacheBuster cacheBuster,
            IOptions<DdsOptions> ddsOptions,
            IOptions<DlcsOptions> dlcsOptions,
            IDds dds,
            IWebHostEnvironment webHostEnvironment
        )
        {
            this.dashboardRepository = dashboardRepository;
            this.cache = cache;
            this.jobRegistry = jobRegistry;
            this.jobProcessor = jobProcessor;
            this.statusProvider = statusProvider;
            this.synchroniser = synchroniser;
            this.recentlyDigitisedIdentifierProvider = recentlyDigitisedIdentifierProvider;
            this.workStorageFactory = workStorageFactory;
            this.cacheBuster = cacheBuster;
            this.ddsOptions = ddsOptions.Value;
            this.dlcsOptions = dlcsOptions.Value;
            this.dds = dds;
            this.webHostEnvironment = webHostEnvironment;
            //this.cachingPackageProvider = cachingPackageProvider;
            //this.cachingAltoSearchTextProvider = cachingAltoSearchTextProvider;
            //this.cachingAllAnnotationProvider = cachingAllAnnotationProvider;
        }

        public ActionResult Index()
        {
            var recent = recentlyDigitisedIdentifierProvider.GetDatedIdentifiers(100);
            return View(recent);
        }

        public async Task<ActionResult> Status(int page = 1)
        {
            var problemJobs = jobRegistry.GetProblems(100);
            Page<ErrorByMetadata> errorsByMetadataPage;
            try
            {
                errorsByMetadataPage = await dashboardRepository.GetErrorsByMetadata(page);
            }
            catch
            {
                // TODO - log this error... introduce logger on refactor
                errorsByMetadataPage = new Page<ErrorByMetadata> { Items = new ErrorByMetadata[] { }, PageNumber = 0, TotalItems = 0, TotalPages = 1 };
            }
            var recentActions = dashboardRepository.GetRecentActions(200);
            var model = new HomeModel
            {
                ProblemJobs = new JobsModel { Jobs = problemJobs.ToArray() },
                ErrorsByMetadataPage = errorsByMetadataPage,
                IngestActions = GetIngestActionDictionary(recentActions),
                BodyInject = ddsOptions.DashBodyInject
            };
            return View(model);
        }

        private Dictionary<string, IngestAction> GetIngestActionDictionary(IEnumerable<IngestAction> recentActions)
        {
            var dict = new Dictionary<string, IngestAction>();
            foreach (var recentAction in recentActions)
            {
                if (recentAction.ManifestationId == null)
                {
                    continue;
                }
                if (dict.ContainsKey(recentAction.ManifestationId))
                {
                    if (dict[recentAction.ManifestationId].Performed < recentAction.Performed)
                    {
                        dict[recentAction.ManifestationId] = recentAction;
                    }
                }
                else
                {
                    dict[recentAction.ManifestationId] = recentAction;
                }
            }
            return dict;
        }

        // GET: Dash
        public async Task<ActionResult> Manifestation(string id)
        {
            var json = AskedForJson();
            var logger = new SmallJobLogger(string.Empty, null);
            logger.Start();
            IDigitisedResource dgResource;
            DdsIdentifier ddsId = null;
            try
            {
                ddsId = new DdsIdentifier(id);
                ViewBag.DdsId = ddsId;
                logger.Log("Start dashboardRepository.GetDigitisedResource(id)");
                dgResource = await dashboardRepository.GetDigitisedResource(id, true);
                logger.Log("Finished dashboardRepository.GetDigitisedResource(id)");
            }
            catch (Exception ex)
            {
                ViewBag.Message = "No digitised resource found for identifier " + id;
                ViewBag.Message += ". ";
                ViewBag.Message += ex.Message;
                if (ddsId != null)
                {
                    ViewBag.TryInstead = ddsId.BNumber;
                }
                if (json)
                {
                    return NotFound(ViewBag.Message);
                }
                return View("ManifestationError");
            }
            
            if (dgResource is IDigitisedManifestation dgManifestation)
            {
                // ***************************************************
                // THIS IS ONLY HERE TO SUPPORT THE PDF LINK
                // IT MUST GO AS SOON AS THE IIIF MANIFEST KNOWS String3
                logger.Log("Start dashboardRepository.FindSequenceIndex(id)");
                // dgManifestation.SequenceIndex = await dashboardRepository.FindSequenceIndex(id);
                logger.Log("Finished dashboardRepository.FindSequenceIndex(id)");
                // represents the set of differences between the METS view of the world and the DLCS view
                logger.Log("Start dashboardRepository.GetDlcsSyncOperation(id)");
                var syncOperation = await dashboardRepository.GetDlcsSyncOperation(dgManifestation, true);
                logger.Log("Finished dashboardRepository.GetDlcsSyncOperation(id)");

                IDigitisedCollection parent;
                IDigitisedCollection grandparent;
                // We need to show the manifestation with information about its parents, it it has any.
                // this allows navigation through multiple manifs
                switch (ddsId.IdentifierType)
                {
                    case IdentifierType.BNumber:
                        parent = null;
                        grandparent = null;
                        break;
                    case IdentifierType.Volume:
                        parent = await GetCachedCollectionAsync(ddsId.BNumber);
                        grandparent = null;
                        break;
                    case IdentifierType.Issue:
                        parent = await GetCachedCollectionAsync(ddsId.VolumePart);
                        grandparent = await GetCachedCollectionAsync(ddsId.BNumber);
                        break;
                    case IdentifierType.BNumberAndSequenceIndex:
                        throw new ArgumentException("id", "Can't use an index-based ID here: " + id);
                    default:
                        throw new ArgumentException("id", "Could not get resource for identifier " + id);
                }

                var skeletonPreview = string.Format(
                    dlcsOptions.SkeletonNamedQueryTemplate, dlcsOptions.CustomerDefaultSpace, id);
                
                var model = new ManifestationModel
                {
                    DefaultSpace = dashboardRepository.DefaultSpace,
                    Url = Url,
                    DdsIdentifier = ddsId,
                    DigitisedManifestation = dgManifestation,
                    Parent = parent,
                    GrandParent = grandparent,
                    SyncOperation = syncOperation,
                    DlcsOptions = dlcsOptions,
                    DlcsSkeletonManifest = skeletonPreview
                };
                model.AVDerivatives = dashboardRepository.GetAVDerivatives(dgManifestation);
                model.MakeManifestationNavData();
                logger.Log("Start dashboardRepository.GetRationalisedJobActivity(syncOperation)");
                var jobActivity = await dashboardRepository.GetRationalisedJobActivity(syncOperation);
                logger.Log("Finished dashboardRepository.GetRationalisedJobActivity(syncOperation)");
                model.IngestJobs = jobActivity.UpdatedJobs;
                model.BatchesForImages = jobActivity.BatchesForCurrentImages;

                model.DbJobIdsToActiveBatches = new Dictionary<int, List<Batch>>();
                foreach (var dlcsIngestJob in model.IngestJobs)
                {
                    if (!model.DbJobIdsToActiveBatches.ContainsKey(dlcsIngestJob.Id))
                    {
                        model.DbJobIdsToActiveBatches[dlcsIngestJob.Id] = new List<Batch>();
                    }
                    logger.Log("Start enumerating job batches");
                    foreach (var dbBatch in dlcsIngestJob.DlcsBatches)
                    {
                        // Move this to the repository
                        if (dbBatch.ResponseBody.HasText())
                        {
                            var reportedBatch = JsonConvert.DeserializeObject<Batch>(dbBatch.ResponseBody);
                            var activeBatch = model.BatchesForImages.SingleOrDefault(b => b.Id == reportedBatch.Id);
                            if (activeBatch != null)
                            {
                                if (!model.DbJobIdsToActiveBatches[dlcsIngestJob.Id].Exists(b => b.Id == activeBatch.Id))
                                {
                                    model.DbJobIdsToActiveBatches[dlcsIngestJob.Id].Add(activeBatch);
                                }
                            }
                        }
                    }
                    logger.Log("Finished enumerating job batches");
                }
                model.IsRunning = syncOperation.DlcsImagesCurrentlyIngesting.Count > 0;

                // TODO - make these client side calls to the same services
                // will speed up dahboard page generation 
                logger.Log("Start cacheBuster queries");
                model.CachedPackageFileInfo = cacheBuster.GetPackageCacheFileInfo(ddsId.BNumber);
                logger.Log("Finished cacheBuster.GetPackageCacheFileInfo(ddsId.BNumber)");
                model.CachedTextModelFileInfo = cacheBuster.GetAltoSearchTextCacheFileInfo(ddsId);
                logger.Log("Finished cacheBuster.GetAltoSearchTextCacheFileInfo(..)");
                model.CachedAltoAnnotationsFileInfo = cacheBuster.GetAllAnnotationsCacheFileInfo(ddsId);
                logger.Log("Finished cacheBuster.GetAllAnnotationsCacheFileInfo(..)");
                ViewBag.Log = LoggingEvent.FromTuples(logger.GetEvents());
                if (json)
                {
                    return AsJson(model.GetJsonModel());
                }
                return View("Manifestation", model);
            }
            if (dgResource is IDigitisedCollection dgCollection)
            {
                // This is the manifestation controller, not the volume or issue controller.
                // So redirect to the first manifestation that we can find for this collection.
                // Put this and any intermediary collections in the short term cache,
                // so that we don't need to build them from scratch after the redirect.
                string redirectId;
                PutCollectionInShortTermCache(dgCollection);
                if (dgCollection.MetsCollection.Manifestations.HasItems())
                {
                    // a normal multiple manifestation, or possibly a periodical volume?
                    if (json)
                    {
                        return RedirectToAction("Collection", "Dash", new { id = ddsId.BNumber, json = "json" });
                    }
                    redirectId = dgCollection.MetsCollection.Manifestations.First().Id;
                    return RedirectToAction("Manifestation", "Dash", new { id = redirectId });
                }
                // a periodical, I think - but go to volume controller for this.
                redirectId = dgCollection.MetsCollection.GetRootId();
                if (json)
                {
                    return RedirectToAction("Collection", "Dash", new { id = redirectId, json = "json" });
                }
                return RedirectToAction("Collection", "Dash", new { id = redirectId });
            }
            ViewBag.Message = "Unknown type of resource found for identifier " + id;
            if (json)
            {
                return NotFound(ViewBag.Message);
            }
            return View("ManifestationError");
        }

        private async Task<IDigitisedCollection> GetCachedCollectionAsync(string identifier)
        {
            // The cache is caching a Task<IDigitisedResource> (from the callback)
            // this works... but we need to revisit
            // TODO: 1) SimpleCache handling of tasks
            // TODO: 2) This should be a request-scoped cache anyway
            var coll = await cache.GetCached(
                CacheSeconds,
                CacheKeyPrefix + identifier,
                async () => (await dashboardRepository.GetDigitisedResource(identifier, true)));
            return (IDigitisedCollection)coll;
        }

        private void PutCollectionInShortTermCache(IDigitisedCollection collection)
        {
            // (in order to PUT this in the cache, we need to retrieve it...
            var key = CacheKeyPrefix + collection.Identifier;
            cache.Remove(key);
            cache.GetCached(CacheSeconds, key, () => collection);
        }

        private ActionResult ShowManifestation(
            IDigitisedManifestation dgManifestation,
            IDigitisedCollection parent,
            IDigitisedCollection grandparent)
        {
            return View(dgManifestation);
        }

        public async Task<ActionResult> Collection(string id)
        {
            var json = AskedForJson();
            IDigitisedCollection collection = null;
            DdsIdentifier ddsId = null;
            bool showError = false;
            try
            {
                ddsId = new DdsIdentifier(id);
                ViewBag.DdsId = ddsId;
                collection = (await dashboardRepository.GetDigitisedResource(id)) as IDigitisedCollection;
            }
            catch (Exception metsEx)
            {
                showError = true;
                ViewBag.MetsError = metsEx;
            }
            if (showError || collection == null)
            {
                ViewBag.Message = "No digitised collection (multiple manifestation) found for identifier " + id;
                if (id != ddsId.BNumber)
                {
                    ViewBag.TryInstead = ddsId.BNumber;
                }
                return View("ManifestationError");
            }
            if (json)
            {
                var simpleCollection = MakeSimpleCollectionModel(collection);
                return AsJson(simpleCollection);
            }
            return View("Collection", collection);
        }

        private SimpleCollectionModel MakeSimpleCollectionModel(IDigitisedCollection collection)
        {
            var simpleCollection = new SimpleCollectionModel();
            if (collection.Collections.HasItems())
            {
                simpleCollection.Collections = new List<SimpleLink>();
                foreach (var coll in collection.Collections)
                {
                    simpleCollection.Collections.Add(new SimpleLink
                    {
                        Label = coll.Identifier + ": " + coll.MetsCollection.Label,
                        Url = Url.Action("Collection", "Dash", new { id = coll.Identifier })
                    });
                }
            }
            if (collection.Manifestations.HasItems())
            {
                simpleCollection.Manifestations = new List<SimpleLink>();
                foreach (var manif in collection.Manifestations)
                {
                    simpleCollection.Manifestations.Add(new SimpleLink
                    {
                        Label = manif.Identifier + ": " + manif.MetsManifestation.Label,
                        Url = Url.Action("Manifestation", "Dash", new { id = manif.Identifier })
                    });
                }
            }
            return simpleCollection;
        }

        public ActionResult ManifestationSearch(string q)
        {
            try
            {
                DdsIdentifier ddsId = new DdsIdentifier(q);
                if (ddsId.IdentifierType == IdentifierType.Volume || ddsId.IdentifierType == IdentifierType.Issue)
                {
                    return RedirectToAction("Manifestation", new { id = ddsId.ToString() });
                }
                // for now, just try to turn it into a b number and redirect
                var bnumber = WellcomeLibraryIdentifiers.GetNormalisedBNumber(q, false);
                return RedirectToAction("Manifestation", new { id = bnumber });
            }
            catch (Exception ex)
            {
                return RedirectToAction("ManifestationSearchError", q);
            }
        }


        // Different actions that all trigger jobs
        public Task<ActionResult> Sync(string id) => CreateAndProcessJobsAsync(id, false, false, "Sync");

        public Task<ActionResult> Resubmit(string id) => CreateAndProcessJobsAsync(id, true, false, "Resubmit");

        public Task<ActionResult> ForceReingest(string id) => CreateAndProcessJobsAsync(id, true, true, "Force reingest");

        public Task<ActionResult> SyncAllManifestations(string id) => CreateAndProcessJobsAsync(id, false, false, "Sync all manifestations");

        public Task<ActionResult> ForceReingestAllManifestations(string id) => CreateAndProcessJobsAsync(id, true, true, "Force reingest of all manifestations");


        private async Task<ActionResult> CreateAndProcessJobsAsync(string id, bool includeIngestingImages, bool forceReingest, string action)
        {
            var jobs = jobRegistry.RegisterImagesForImmediateStartAsync(id);
            await foreach (var job in jobs)
            {
                dashboardRepository.LogAction(job.GetManifestationIdentifier(), job.Id, User.Identity.Name, action);
                await jobProcessor.ProcessJobAsync(job, includeIngestingImages, forceReingest, true);
            }
            synchroniser.RefreshFlatManifestations(id);
            return RedirectToAction("Manifestation", new { id });
        }

        public ActionResult Jobs()
        {
            var jobs = jobRegistry.GetJobs(500);
            var model = new JobsModel { Jobs = jobs.ToArray() };
            return View(model);
        }

        public ActionResult Queue()
        {
            var queue = jobRegistry.GetQueue(statusProvider.EarliestJobToTake);
            var model = new JobsModel { Jobs = queue.ToArray() };
            return View(model);
        }



        public ActionResult AssetType(string id = "video/mpeg")
        {
            var type = id.ReplaceFirst("-", "/");
            var model = new AssetTypeModel
            {
                Type = type,
                FlatManifestations = dds.GetByAssetType(type),
                TotalsByAssetType = dds.GetTotalsByAssetType()
            };
            return View(model);
        }


        public ActionResult RecentActions()
        {
            var recentActions = dashboardRepository.GetRecentActions(200);
            return View(recentActions);
        }

        public async Task<ActionResult> StopStatus()
        {
            ViewBag.Message = "Your application description page.";
            ViewBag.RunProcesses = await statusProvider.ShouldRunProcesses();
            DateTime cutoff = statusProvider.LatestJobToTake ?? DateTime.Now;
            ViewBag.JobDelay = (DateTime.Now - cutoff).Minutes;
            return View("StopStatus");
        }

        public ActionResult Job(int id)
        {
            var job = jobRegistry.GetJob(id);
            var model = new JobsModel { Jobs = new[] { job } };
            return View(model);
        }


        public ActionResult UV(string id)
        {
            return View(dashboardRepository.GetBNumberModel(id, id));
        }


        public async Task<ActionResult> StorageMap(string id, string resolveRelativePath = null)
        {
            var archiveStore = (ArchiveStorageServiceWorkStore) await workStorageFactory.GetWorkStore(id);
            if (!resolveRelativePath.HasText())
            {
                resolveRelativePath = id + ".xml";
            }
            var model = new StorageMapModel
            {
                BNumber = id,
                StorageMap = archiveStore.ArchiveStorageMap,
                PathToResolve = resolveRelativePath
            };
            try
            {
                model.ResolvedAwsKey = archiveStore.GetAwsKey(resolveRelativePath);
            }
            catch (Exception ex)
            {
                model.ErrorMessage = ex.Message;
            }
            return View(model);
        }


        public ActionResult CacheBust(string id)
        {
            var bNumber = new DdsIdentifier(id).BNumber;

            // we won't clean this up, for now. Sorry.
            //cachingDipProvider.DeleteDipCacheFile(bNumber);
            bool success = true;
            string message = null;
            CacheBustResult cacheBustResult = null;
            try
            {
                cacheBustResult = cacheBuster.BustPackage(bNumber);
            }
            catch (Exception ex)
            {
                success = false;
                message = ex.Message;
            }
            TempData["CacheBustResult"] = new DeleteResult
            {
                Success = success,
                Message = message,
                CacheBustResult = cacheBustResult
            };
            return RedirectToAction("Manifestation", new { id });
        }

        public ActionResult CacheBustAlto(string id)
        {
            var seqIndex = dashboardRepository.FindSequenceIndex(id);
            var ddsId = new DdsIdentifier(id);
            var textCbr = cacheBuster.BustAltoSearchText(ddsId.BNumber, seqIndex);
            cacheBuster.BustAllAnnotations(ddsId.BNumber, seqIndex);
            TempData["AltoCacheBustResult"] = new DeleteResult { Success = true, CacheBustResult = textCbr };
            dashboardRepository.LogAction(id, null, User.Identity.Name, "Cache Bust Alto");
            return RedirectToAction("Manifestation", new { id });
        }

        public async Task<ActionResult> DeletePdf(string id)
        {
            var success = await dashboardRepository.DeletePdf(id);
            var message = success ? "success" : "failed";
            TempData["delete-pdf"] = success;
            dashboardRepository.LogAction(id, null, User.Identity.Name, $"Delete PDF: {message}");
            return RedirectToAction("Manifestation", new { id });
        }

        public async Task<ActionResult> DoStop(object id)
        {
            dashboardRepository.LogAction(null, null, User.Identity.Name, "STOP services");
            TempData["stop-result"] = await statusProvider.Stop();
            return RedirectToAction("StopStatus");
        }

        public async Task<ActionResult> DoStart(object id)
        {
            dashboardRepository.LogAction(null, null, User.Identity.Name, "START services");
            TempData["start-result"] = await statusProvider.Start();
            return RedirectToAction("StopStatus");
        }

        public async Task<ActionResult> CleanOldJobs(string id)
        {
            int removed = await dashboardRepository.RemoveOldJobs(id);
            TempData["remove-old-jobs"] = removed;
            return RedirectToAction("Manifestation", new { id });
        }



        public Task<Dictionary<string, long>> GetDlcsQueueLevel()
        {
            return dashboardRepository.GetDlcsQueueLevel();
        }

        public async Task<ActionResult> DeleteOrphans(string id)
        {
            dashboardRepository.LogAction(id, null, User.Identity.Name, "Delete Orphans");
            int removed = await dashboardRepository.DeleteOrphans(id);
            TempData["orphans-deleted"] = removed;
            return RedirectToAction("Manifestation", new { id });
        }


        public ActionResult TailJobProcessor(int id = 60)
        {
            var model = GetLogTailModel(ddsOptions.JobProcessorLog, id);
            return View(model);
        }


        public ActionResult TailWorkflowProcessor(int id = 60)
        {
            var model = GetLogTailModel(ddsOptions.WorkflowProcessorLog, id);
            return View(model);
        }

        private string[] GetLogTailModel(string path, int lines)
        {
            ViewBag.LogFilePath = path;
            string[] model = { "Unable to read log at " + path };
            if (path != null && System.IO.File.Exists(path))
            {
                // not suitable for huge log files, but simple
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    List<string> file = new List<string>();
                    while (!sr.EndOfStream)
                    {
                        file.Add(sr.ReadLine());
                    }
                    var log = file.ToArray();
                    var start = Math.Max(0, log.Length - lines);
                    model = log.Skip(start).ToArray();
                }
            }
            return model;
        }

        public JsonResult AutoComplete(string id)
        {
            var suggestions = dds.AutoComplete(id);
                return Json(suggestions.Select(fm => new AutoCompleteSuggestion
                {
                    id = fm.PackageIdentifier,
                    label = fm.Label
                }).ToArray());
        }

        public ActionResult ManifestationSearchError(string q)
        {
            return View(q);
        }

        public bool AskedForJson()
        {
            // TODO: this should be done in a .NET Core way
            if(Request.GetTypedHeaders().Accept.Any(a => a.MediaType == "application/json"))
            {
                return true;
            }
            if (Request.QueryString.Value.Contains("json"))
            {
                return true;
            }
            return false;
        }

        public ActionResult AsJson(object model)
        {
            var result = JsonConvert.SerializeObject(model, Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
            return Content(result, "application/json");
        }

        public async Task<ActionResult> StorageIngest(string id)
        {
            var archivalStorageFactory = workStorageFactory as ArchiveStorageServiceWorkStorageFactory;
            if (archivalStorageFactory == null)
            {
                throw new NotSupportedException("This page only works with the new storage service");
            }
            var ingest = await archivalStorageFactory.GetIngest(id);
            return View(Models.Ingest.FromJObject(ingest));
        }

        public ActionResult Settings()
        {
            var appSettings = "appsettings.json";
            var appSettingsEnv = $"appsettings.{webHostEnvironment.EnvironmentName}.json";
            var settings = new SettingsModel
            {
                AppSettings = System.IO.File.ReadAllText(appSettings),
                AppSettingsForEnvironment = System.IO.File.ReadAllText(appSettingsEnv)
            };
            return View(settings);
        }
    }

    public class AutoCompleteSuggestion
    {
        public string id { get; set; }

        public string label { get; set; }
    }


    public class AssetTypeModel
    {
        public string Type { get; set; }
        public List<Manifestation> FlatManifestations { get; set; }
        public Dictionary<string, int> TotalsByAssetType { get; set; }
    }

    /// <summary>
    /// for use with legacy dashboard call
    /// </summary>
    public class DeleteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public CacheBustResult CacheBustResult { get; set; }
    }

    public class SettingsModel
    {
        public string AppSettings { get; set; }
        public string AppSettingsForEnvironment { get; set; }
    }
}