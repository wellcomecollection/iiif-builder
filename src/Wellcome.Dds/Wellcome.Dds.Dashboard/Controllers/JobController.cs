using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.Dashboard.Models;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class JobController : Controller
    {
        private readonly IIngestJobRegistry jobRegistry;
        private readonly IIngestJobProcessor jobProcessor;
        private readonly Synchroniser synchroniser;
        private readonly IDashboardRepository dashboardRepository;

        public JobController(
            IIngestJobRegistry jobRegistry,
            IIngestJobProcessor jobProcessor,
            Synchroniser synchroniser,
            IDashboardRepository dashboardRepository)
        {
            this.jobRegistry = jobRegistry;
            this.jobProcessor = jobProcessor;
            this.synchroniser = synchroniser;
            this.dashboardRepository = dashboardRepository;
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
            int removed = await dashboardRepository.RemoveOldJobs(id);
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
            var jobs = jobRegistry.RegisterImagesForImmediateStart(id);
            await foreach (var job in jobs)
            {
                dashboardRepository.LogAction(job.GetManifestationIdentifier(), job.Id, User.Identity.Name, action);
                await jobProcessor.ProcessJobAsync(job, includeIngestingImages, forceReingest, true);
            }
            synchroniser.RefreshFlatManifestations(id);
            return RedirectToAction("Manifestation", "Dash", new { id });
        }
    }
}