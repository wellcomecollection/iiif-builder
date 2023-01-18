using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Ingest
{
    public class CloudServicesIngestRegistry : IIngestJobRegistry
    {
        private readonly IMetsRepository metsRepository;
        private readonly DdsInstrumentationContext ddsInstrumentationContext;
        private readonly ILogger<CloudServicesIngestRegistry> logger;

        public CloudServicesIngestRegistry(
            IMetsRepository metsRepository,
            DdsInstrumentationContext ddsInstrumentationContext,
            ILogger<CloudServicesIngestRegistry> logger)
        {
            this.metsRepository = metsRepository;
            this.ddsInstrumentationContext = ddsInstrumentationContext;
            this.logger = logger;
        }

        public async Task<IEnumerable<DlcsIngestJob>> GetRecentJobs(int number)
        {
            return await ddsInstrumentationContext.DlcsIngestJobs
                .OrderByDescending(j => j.Created)
                .Take(number)
                .Include(j => j.DlcsBatches)
                .ToArrayAsync(); 
        }

        public Task<DlcsIngestJob?> GetJob(int id)
        {
            return ddsInstrumentationContext.DlcsIngestJobs
                .Include(j => j.DlcsBatches)
                .SingleOrDefaultAsync(j => j.Id == id);
        }

        public async Task<IEnumerable<DlcsIngestJob>> GetQueue(DateTime? after)
        {
            var query = ddsInstrumentationContext.DlcsIngestJobs
                .OrderByDescending(j => j.Created)
                .Include(j => j.DlcsBatches)
                .Where(j => j.StartProcessed == null);

            if (after.HasValue)
            {
                return query.Where(j => j.Created > after.Value).ToArray();
            }
            return await query.ToArrayAsync();
        }

        public async Task<IEnumerable<DlcsIngestJob>> GetProblems(int maxToFetch = 100)
        {
            // .NET Core migration note.
            // I have removed the BatchWithJob class as it was only used internally to materialise 
            // query results from the sqlServer query below... this method still just returns
            // IEnumerable<DlcsIngestJob>
            // So we need to re-write the way it gets jobs with problems.
            // 
            // Most recent 100 DlcsIngestJobs 
            // that have Succeeded=0, or have a batch with ErrorText not null
            // where batches have ContentLength > 0

            var problems = ddsInstrumentationContext.DlcsIngestJobs
                .Where(j => j.DlcsBatches.All(b => b.ContentLength > 0))
                .Where(j => !j.Succeeded || j.DlcsBatches.Any(b => b.ErrorText != null))
                .OrderByDescending(j => j.Id)
                .Take(maxToFetch);

            return await problems.ToArrayAsync();

            // Please see 
            // https://github.com/wellcomelibrary/dds-ecosystem/blob/new-storage-service/wellcome-dds/Wellcome.Dds/Ingest/CloudServicesIngestRegistry.cs#L78
            // for comparison         
        }

        private DlcsIngestJob NewJob(string identifier, string label, int sequenceIndex, string? volumeIdentifier,
            string? issueIdentifier, bool useInitialOrigin, bool immediateStart)
        {
            var job = new DlcsIngestJob(identifier)
            {
                Label = label,
                SequenceIndex = sequenceIndex,
                VolumePart = volumeIdentifier,
                IssuePart = issueIdentifier
            };

            if (useInitialOrigin)
            {
                job.Data = "initialOrigin";
            }
            if (immediateStart)
            {
                job.StartProcessed = DateTime.Now;
            }
            return job;
        }

        // TODO: Making this async needs a code review (compare CloudServicesIngestRegistry and its interface with dds-ecosystem)
        // The following few mthods all need reviewing
        public async IAsyncEnumerable<DlcsIngestJob> RegisterImagesForImmediateStart(DdsIdentifier identifier)
        {
            await foreach (var job in RegisterImagesInternal(identifier, false, true))
            {
                yield return job;
            }
        }

        public async Task<DlcsIngestJob[]> RegisterImages(DdsIdentifier identifier, bool useInitialOrigin = false)
        {
            // Can this not return an array? Why does it need to?
            var jobs = new List<DlcsIngestJob>();
            await foreach (var job in RegisterImagesInternal(identifier, useInitialOrigin, false))
            {
                jobs.Add(job);
            }
            return jobs.ToArray();
        }

        private async IAsyncEnumerable<DlcsIngestJob> RegisterImagesInternal(DdsIdentifier identifier, bool useInitialOrigin, bool immediateStart)
        {
            await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(identifier))
            {
                if (manifestationInContext.PackageIdentifier.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("Can't create a job without a package identifier");
                }
                var job = NewJob(
                    manifestationInContext.PackageIdentifier,
                    manifestationInContext.Manifestation?.Label ?? manifestationInContext.PackageIdentifier,
                    manifestationInContext.SequenceIndex,
                    manifestationInContext.VolumeIdentifier,
                    manifestationInContext.IssueIdentifier,
                    useInitialOrigin, immediateStart);
                AddNewJob(job);
                yield return job;
            }
        }

        private void AddNewJob(DlcsIngestJob job)
        {
            // remove any unstarted jobs with the same settings
            var existingQuery = ddsInstrumentationContext.DlcsIngestJobs.Where(j =>
                j.StartProcessed == null
                && j.Identifier == job.Identifier
                && j.SequenceIndex == job.SequenceIndex);
            if (job.VolumePart != null)
            {
                existingQuery = existingQuery.Where(j => j.VolumePart == job.VolumePart);
            }
            if (job.IssuePart != null)
            {
                existingQuery = existingQuery.Where(j => j.IssuePart == job.IssuePart);
            }
            ddsInstrumentationContext.DlcsIngestJobs.RemoveRange(existingQuery);
            // now add the new one
            logger.LogDebug("Adding a new DlcsIngestJob for {identifier}", job.Identifier);
            ddsInstrumentationContext.DlcsIngestJobs.Add(job);
            try
            {
                // Is this problematic?
                // If called from the dashboard, this is the only SaveChanges() that will happen.
                // But if called in a workflow job, the job's overall SaveChanges will be called as well
                ddsInstrumentationContext.SaveChanges();
            }
            catch (DbUpdateException e)
            {
                throw new DdsInstrumentationDbException("Could not save new DLCS Ingest Jobs: " + e.Message, e);
            }
        }
    }
}
