using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Utils;
using Utils.Logging;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class DashController : Controller
    {
        private readonly IDigitalObjectRepository digitalObjectRepository;
        private readonly IIngestJobRegistry jobRegistry;
        private readonly IStatusProvider statusProvider;
        private readonly IDatedIdentifierProvider recentlyDigitisedIdentifierProvider;
        private readonly IWorkStorageFactory workStorageFactory;
        private readonly IDds dds;
        private readonly StorageServiceClient storageServiceClient;
        private readonly ILogger<DashController> logger;
        private readonly UriPatterns uriPatterns;
        private readonly ManifestationModelBuilder modelBuilder;
        private readonly IWorkflowCallRepository workflowCallRepository;
        private readonly IStorage storage;
        private readonly DdsOptions ddsOptions;
        
        public DashController(
            IDigitalObjectRepository digitalObjectRepository,
            IIngestJobRegistry jobRegistry,
            IStatusProvider statusProvider,
            IDatedIdentifierProvider recentlyDigitisedIdentifierProvider,
            IWorkStorageFactory workStorageFactory,
            IDds dds,
            StorageServiceClient storageServiceClient,
            ILogger<DashController> logger,
            UriPatterns uriPatterns,
            ManifestationModelBuilder modelBuilder,
            IWorkflowCallRepository workflowCallRepository,
            IStorage storage,
            IOptions<DdsOptions> options
        )
        {
            // TODO - we need a review of all these dependencies!
            // too many things going on in this controller
            this.digitalObjectRepository = digitalObjectRepository;
            this.jobRegistry = jobRegistry;
            this.statusProvider = statusProvider;
            this.recentlyDigitisedIdentifierProvider = recentlyDigitisedIdentifierProvider;
            this.workStorageFactory = workStorageFactory;
            this.dds = dds;
            this.storageServiceClient = storageServiceClient;
            this.logger = logger;
            this.uriPatterns = uriPatterns;
            this.modelBuilder = modelBuilder;
            this.workflowCallRepository = workflowCallRepository;
            this.storage = storage;
            ddsOptions = options.Value;
        }

        public ActionResult Index()
        {
            var recent = recentlyDigitisedIdentifierProvider.GetDatedIdentifiers(100);
            return View(recent);
        }

        public async Task<ActionResult> Status(int page = 1)
        {
            var problemJobs = await jobRegistry.GetProblems(100);
            Page<ErrorByMetadata> errorsByMetadataPage;
            try
            {
                errorsByMetadataPage = await digitalObjectRepository.GetErrorsByMetadata(
                    page, new DlcsCallContext("DashController::Status", "[no-id]"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting errorsByMetadata for page {page}", page);
                errorsByMetadataPage = new Page<ErrorByMetadata>
                    {Items = new ErrorByMetadata[] { }, PageNumber = 0, TotalItems = 0, TotalPages = 1};
            }

            var recentActions = digitalObjectRepository.GetRecentActions(200);
            var model = new HomeModel
            {
                ProblemJobs = new JobsModel {Jobs = problemJobs.ToArray()},
                ErrorsByMetadataPage = errorsByMetadataPage,
                IngestActions = GetIngestActionDictionary(recentActions)
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
            DdsIdentifier ddsId = null;
            logger.LogDebug("Generating Manifestation Page for {identifier}", id);
            try
            {
                ddsId = new DdsIdentifier(id);
                ViewBag.DdsId = ddsId;
                var result = await modelBuilder.Build(ddsId, Url);

                // if we have a model render it
                if (result.Model != null)
                {
                    ViewBag.Log = LoggingEvent.FromTuples(modelBuilder.GetLoggingEvents());
                    return View("Manifestation", result.Model);
                }
                
                // we don't have a model so must be a redirect 
                if (result.RedirectToCollection)
                {
                    return RedirectToAction("Collection", "Dash", new {id = result.RedirectId});
                }
                
                if (result.RedirectToManifest)
                {
                    return RedirectToAction("Manifestation", "Dash", new {id = result.RedirectId});
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting manifestation for '{id}'", id);
                ViewBag.Message = $"No digitised resource found for identifier {id}. {ex.Message}";
                if (ddsId != null)
                {
                    ViewBag.TryInstead = ddsId.PackageIdentifierPathElementSafe;
                }
            }
            
            // At this point we need to return the error page. See if there's a workflow job for this identifier:
            if (ddsId != null)
            {
                ViewBag.WorkflowJob = await workflowCallRepository.GetWorkflowJob(ddsId.PackageIdentifier);
            }
            
            return View("ManifestationError");
        }

        public async Task<ActionResult> Collection(string id)
        {
            var json = AskedForJson();
            IDigitalCollection collection = null;
            DdsIdentifier ddsId = null;
            bool showError = false;
            try
            {
                var dlcsCallContext = new DlcsCallContext("DashController::Collection", id);
                logger.LogDebug("Starting DlcsCallContext: {callContext}", dlcsCallContext);
                ddsId = new DdsIdentifier(id);
                ViewBag.DdsId = ddsId;
                collection = await digitalObjectRepository.GetDigitalObject(
                    id, dlcsCallContext) as IDigitalCollection;
            }
            catch (Exception metsEx)
            {
                showError = true;
                ViewBag.MetsError = metsEx;
            }

            if (showError || collection == null)
            {
                ViewBag.Message = "No digitised collection (multiple manifestation) found for identifier " + id;
                if (id != ddsId.PackageIdentifier)
                {
                    ViewBag.TryInstead = ddsId.PackageIdentifier;
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

        private SimpleCollectionModel MakeSimpleCollectionModel(IDigitalCollection collection)
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
                        Url = Url.Action("Collection", "Dash", new {id = coll.Identifier})
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
                        Url = Url.Action("Manifestation", "Dash", new {id = manif.Identifier})
                    });
                }
            }

            return simpleCollection;
        }

        public ActionResult ManifestationSearch(string q)
        {
            if (Request.Form["luckydip"] == "true")
            {
                var random = dds.AutoComplete("imfeelinglucky").First();
                return RedirectToAction("Manifestation", new { id = random.Id });
            }
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
            catch (Exception)
            {
                return RedirectToAction("ManifestationSearchError", q);
            }
        }
        
        public async Task<ActionResult> Queue()
        {
            var queue = await jobRegistry.GetQueue(statusProvider.EarliestJobToTake);
            var model = new JobsModel { Jobs = queue.ToArray() };
            return View(model);
        }

        public async Task<ActionResult> AssetType(string id = "video/mpeg")
        {
            var type = id.ReplaceFirst("-", "/");
            var model = new AssetTypeModel
            {
                Type = type,
                FlatManifestations = dds.GetByAssetType(type),
                TotalsByAssetType = dds.GetTotalsByAssetType()
            };
            if (type == "application/pdf")
            {
                const string prefix = "_pdf_thumbs/";
                var pdfThumbs = await storage.GetFiles(ddsOptions.PresentationContainer!, prefix);
                model.Thumbnails = pdfThumbs.ToDictionary(f => f.Path.RemoveStart(prefix).RemoveEnd(".jpg"));
            }
            return View(model);
        }

        public ActionResult RecentActions()
        {
            var recentActions = digitalObjectRepository.GetRecentActions(200);
            return View(recentActions);
        }

        public async Task<ActionResult> StopStatus()
        {
            ViewBag.Message = "Your application description page.";
            ViewBag.RunProcesses = await statusProvider.ShouldRunProcesses();
            DateTime cutoff = statusProvider.LatestJobToTake ?? DateTime.UtcNow;
            ViewBag.JobDelay = (DateTime.UtcNow - cutoff).Minutes;
            return View("StopStatus");
        }

        public ActionResult UV(string id, int version)
        {
            var ddsId = new DdsIdentifier(id);
            var manifest = uriPatterns.Manifest(ddsId);
            if (version == 2)
            {
                manifest = manifest.Replace("/presentation/", "/presentation/v2/");
            }
            return Redirect("https://universalviewer.io/examples/#?manifest=" + manifest);
        }
        
        public ActionResult UVPreview(string id, int version, string origin)
        {
            var action = version == 2 ? "IIIF2Raw" : "IIIFRaw";
            var previewUri = $"{origin}{Url.Action(action, "Peek", new { id })}";
            return Redirect("https://universalviewer.io/examples/#?manifest=" + previewUri);
        }
        
        public IActionResult Mirador(string id)
        {
            var ddsId = new DdsIdentifier(id);
            var manifests = new List<string>{ uriPatterns.Manifest(ddsId) };
            if (ddsId.StorageSpace == "digitised")
            {
                // This isn't actually the criteria for IIIF v2 but it will suffice here
                manifests.Add(manifests[0].Replace("/presentation/", "/presentation/v2/"));
            }
            return View("Mirador", manifests);
        }
        
        public IActionResult MiradorPreview(string id, string origin)
        {
            var ddsId = new DdsIdentifier(id);
            var manifests = new List<string>{ $"{origin}{Url.Action("IIIFRaw", "Peek", new { id })}" };
            if (ddsId.StorageSpace == "digitised")
            {
                manifests.Add($"{origin}{Url.Action("IIIF2Raw", "Peek", new { id })}");
            }
            return View("Mirador", manifests);
        }

        public IActionResult Validator(string id)
        {
            // no point trying to validate a Wellcome V2 manifest
            var validator = "http://iiif.io/api/presentation/validator/service/validate?format=json&version=3.0&url=";
            return Redirect(validator + uriPatterns.Manifest(id));
        }

        public async Task<ActionResult> StorageMap(string id, string resolveRelativePath = null)
        {
            // NOTE - is this leaky if it knows underlying implementation? If it needs to would GetWorkStore<T> work?
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

        public async Task<ActionResult> DeletePdf(string id)
        {
            var success = await digitalObjectRepository.DeletePdf(id);
            var message = success ? "success" : "failed";
            TempData["delete-pdf"] = success;
            digitalObjectRepository.LogAction(id, null, User.Identity.Name, $"Delete PDF: {message}");
            return RedirectToAction("Manifestation", new { id });
        }

        public async Task<ActionResult> DoStop(object id)
        {
            digitalObjectRepository.LogAction(null, null, User.Identity.Name, "STOP services");
            TempData["stop-result"] = await statusProvider.Stop();
            return RedirectToAction("StopStatus");
        }

        public async Task<ActionResult> DoStart(object id)
        {
            digitalObjectRepository.LogAction(null, null, User.Identity.Name, "START services");
            TempData["start-result"] = await statusProvider.Start();
            return RedirectToAction("StopStatus");
        }

        public Task<Dictionary<string, long>> GetDlcsQueueLevel()
        {
            return digitalObjectRepository.GetDlcsQueueLevel();
        }

        public async Task<ActionResult> DeleteOrphans(string id)
        {
            digitalObjectRepository.LogAction(id, null, User.Identity.Name, "Delete Orphans");
            int removed = await digitalObjectRepository.DeleteOrphans(id, new DlcsCallContext("DashController::DeleteOrphans", id));
            TempData["orphans-deleted"] = removed;
            return RedirectToAction("Manifestation", new { id });
        }

        public JsonResult AutoComplete(string id)
        {
            var suggestions = dds.AutoComplete(id);
                return Json(suggestions.Select(fm => new AutoCompleteSuggestion
                {
                    id = fm.PackageIdentifier,
                    label = fm.PackageLabel
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
            var ingest = await storageServiceClient.GetIngest(id);
            return View(Ingest.FromJObject(ingest));
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
        public Dictionary<string, long> TotalsByAssetType { get; set; }
        
        public Dictionary<string, ISimpleStoredFileInfo> Thumbnails { get; set; }
    }

    /// <summary>
    /// for use with legacy dashboard call
    /// </summary>
    public class DeleteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class OverallResult
    {
        public ManifestationModel Model { get; set; }
        public bool RedirectToCollection { get; set; }
        public bool RedirectToManifest { get; set; }
        public string RedirectId { get; set; }
    }
}