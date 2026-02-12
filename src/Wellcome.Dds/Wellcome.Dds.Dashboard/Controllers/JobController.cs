using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Models;
using Wellcome.Dds.Repositories;

namespace Wellcome.Dds.Dashboard.Controllers
{
    /// <summary>
    /// Contains operations related to dlcs-jobs
    /// </summary>
    public class JobController : Controller
    {
        private readonly IIngestJobRegistry jobRegistry;
        private readonly IIngestJobProcessor jobProcessor;
        private readonly Synchroniser synchroniser;
        private readonly IDigitalObjectRepository digitalObjectRepository;
        private readonly ILogger<JobController> logger;
        private readonly IIdentityService identityService;

        public JobController(
            IIngestJobRegistry jobRegistry,
            IIngestJobProcessor jobProcessor,
            Synchroniser synchroniser,
            IDigitalObjectRepository digitalObjectRepository,
            ILogger<JobController> logger,
            IIdentityService identityService)
        {
            this.jobRegistry = jobRegistry;
            this.jobProcessor = jobProcessor;
            this.synchroniser = synchroniser;
            this.digitalObjectRepository = digitalObjectRepository;
            this.logger = logger;
            this.identityService = identityService;
        }
        
        // Different actions that all trigger jobs
        public Task<ActionResult> Sync(string id) 
            => CreateAndProcessJobs(id, false, false, "Sync");

        public Task<ActionResult> Resubmit(string id) 
            => CreateAndProcessJobs(id, true, false, "Resubmit");

        public Task<ActionResult> ForceReingest(string id) 
            => CreateAndProcessJobs(id, true, true, "Force reingest");

        public Task<ActionResult> SyncAllManifestations(string id) 
            => CreateAndProcessJobs(id, false, false, "Sync all manifestations");

        public Task<ActionResult> ForceReingestAllManifestations(string id) 
            => CreateAndProcessJobs(id, true, true, "Force reingest of all manifestations");
        
        public async Task<ActionResult> CleanOldJobs(string id)
        {
            int removed = await digitalObjectRepository.RemoveOldJobs(id);
            TempData["remove-old-jobs"] = removed;
            return RedirectToAction("Manifestation", "Dash", new { id });
        }
        
        public async Task<ActionResult> Index(int id)
        {
            var job = await jobRegistry.GetJob(id);
            var model = new JobsModel
            {
                Jobs = new[] { job },
                ManifestationIdentifiers = new Dictionary<int, DdsIdentity>()
            };
            if (job != null)
            {
                model.ManifestationIdentifiers[job.Id] = identityService.GetIdentity(job.GetManifestationIdentifier());
            }
            return View(model);
        }
        
        public async Task<ActionResult> Recent()
        {
            var recents = await jobRegistry.GetRecentJobs(500);
            var jobs = recents.ToArray();
            var model = new JobsModel
            {
                Jobs = jobs,
                ManifestationIdentifiers = jobs.ToDictionary(
                    j => j.Id, 
                    j => identityService.GetIdentity(j.GetManifestationIdentifier()))
            };
            return View(model);
        }
        
        private async Task<ActionResult> CreateAndProcessJobs(string id, bool includeIngestingImages, bool forceReingest, string action)
        {
            logger.LogDebug("Creating and immediately processing a job for {identifier}", id);
            var ddsId = identityService.GetIdentity(id);
            
            var jobs = jobRegistry.RegisterImagesForImmediateStart(ddsId);
            await foreach (var job in jobs)
            {
                digitalObjectRepository.LogAction(job.GetManifestationIdentifier(), job.Id, User.Identity.Name, action);
                await jobProcessor.ProcessJob(job, includeIngestingImages, forceReingest, true);
            }

            try
            {
                await synchroniser.RefreshDdsManifestations(ddsId);
            }
            catch (ArgumentException ae)
            {
                TempData["no-work-synchronisation"] = ae.Message;
            }
            return RedirectToAction("Manifestation", "Dash", new { ddsId.PathElementSafe });
        }
    }
}