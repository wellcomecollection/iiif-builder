﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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

        public JobController(
            IIngestJobRegistry jobRegistry,
            IIngestJobProcessor jobProcessor,
            Synchroniser synchroniser,
            IDigitalObjectRepository digitalObjectRepository)
        {
            this.jobRegistry = jobRegistry;
            this.jobProcessor = jobProcessor;
            this.synchroniser = synchroniser;
            this.digitalObjectRepository = digitalObjectRepository;
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
            var model = new JobsModel { Jobs = new[] { job } };
            return View(model);
        }
        
        public async Task<ActionResult> Recent()
        {
            var jobs = await jobRegistry.GetRecentJobs(500);
            var model = new JobsModel { Jobs = jobs.ToArray() };
            return View(model);
        }
        
        private async Task<ActionResult> CreateAndProcessJobs(string id, bool includeIngestingImages, bool forceReingest, string action)
        {
            // TODO: handle different ID forms in jobs table; at present this goes in as null because uses .BNumber
            var ddsId = new DdsIdentifier(id);
            if (!ddsId.HasBNumber)
            {
                throw new NotSupportedException("You can only create jobs for b numbers at the moment.");
            }
            
            
            var jobs = jobRegistry.RegisterImagesForImmediateStart(id);
            await foreach (var job in jobs)
            {
                digitalObjectRepository.LogAction(job.GetManifestationIdentifier(), job.Id, User.Identity.Name, action);
                await jobProcessor.ProcessJob(job, includeIngestingImages, forceReingest, true);
            }

            try
            {
                await synchroniser.RefreshDdsManifestations(id);
            }
            catch (ArgumentException ae)
            {
                TempData["no-work-synchronisation"] = ae.Message;
            }
            return RedirectToAction("Manifestation", "Dash", new { id });
        }
    }
}