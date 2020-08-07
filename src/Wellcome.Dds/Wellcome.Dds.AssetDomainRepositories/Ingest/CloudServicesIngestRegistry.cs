using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Ingest
{

    public class CloudServicesIngestRegistry : IIngestJobRegistry
    {
        private readonly IMetsRepository metsRepository;
        private readonly DdsInstrumentationContext ddsInstrumentationContext;

        public CloudServicesIngestRegistry(
            IMetsRepository metsRepository,
            DdsInstrumentationContext ddsInstrumentationContext)
        {
            this.metsRepository = metsRepository;
            this.ddsInstrumentationContext = ddsInstrumentationContext;
        }

        public IEnumerable<DlcsIngestJob> GetJobs(int number)
        {
            return ddsInstrumentationContext.DlcsIngestJobs
                .OrderByDescending(j => j.Created)
                .Take(number)
                .Include(j => j.DlcsBatches)
                .ToArray(); // The operation cannot be completed because the DbContext has been disposed. Want to defer this
        }

        public DlcsIngestJob GetJob(int id)
        {
            return ddsInstrumentationContext.DlcsIngestJobs
                .Include(j => j.DlcsBatches)
                .SingleOrDefault(j => j.Id == id);
        }

        public IEnumerable<DlcsIngestJob> GetQueue(DateTime? after)
        {
            var query = ddsInstrumentationContext.DlcsIngestJobs
                .OrderByDescending(j => j.Created)
                .Include(j => j.DlcsBatches)
                .Where(j => j.StartProcessed == null);

            if (after.HasValue)
            {
                return query.Where(j => j.Created > after.Value).ToArray();
            }
            return query.ToArray();
        }

        public IEnumerable<DlcsIngestJob> GetProblems(int maxToFetch = 100)
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

            return problems.ToArray();

            // Please see 
            // https://github.com/wellcomelibrary/dds-ecosystem/blob/new-storage-service/wellcome-dds/Wellcome.Dds/Ingest/CloudServicesIngestRegistry.cs#L78
            // for comparison         
        }

        private DlcsIngestJob NewJob(string identifier, string label, int sequenceIndex, string volumeIdentifier,
            string issueIdentifier, bool useInitialOrigin, bool immediateStart)
        {
            var job = new DlcsIngestJob
            {
                Created = DateTime.Now,
                Identifier = identifier,
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
        public async IAsyncEnumerable<DlcsIngestJob> RegisterImagesForImmediateStart(string identifier)
        {
            await foreach (var job in RegisterImagesInternal(identifier, false, true))
            {
                yield return job;
            }
        }

        public async Task<DlcsIngestJob[]> RegisterImages(string identifier, bool useInitialOrigin = false)
        {
            // Can this not return an array? Why does it need to?
            var jobs = new List<DlcsIngestJob>();
            await foreach (var job in RegisterImagesInternal(identifier, useInitialOrigin, false))
            {
                jobs.Add(job);
            }
            return jobs.ToArray();
        }

        private async IAsyncEnumerable<DlcsIngestJob> RegisterImagesInternal(string identifier, bool useInitialOrigin, bool immediateStart)
        {
            // TODO - The unstarted old jobs are cleaned out in AddNewJob(..)
            // But wouldn't it be better to clean them all out here? Before creating the set of new
            // jobs for this b number (will be a set of jobs if multi volume)?
            await foreach (var manifestationInContext in metsRepository.GetAllManifestationsInContext(identifier))
            {
                var job = NewJob(
                    manifestationInContext.BNumber,
                    manifestationInContext.Manifestation?.Label ?? manifestationInContext.BNumber,
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
            // TODO: does this need to be in a transaction?
            // https://github.com/wellcomecollection/iiif-builder/pull/17#discussion_r453693665
            using (var trans = ddsInstrumentationContext.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
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
                    foreach (var dlcsIngestJob in existingQuery)
                    {
                        ddsInstrumentationContext.DlcsIngestJobs.Remove(dlcsIngestJob);
                    }
                    // now add the new one
                    ddsInstrumentationContext.DlcsIngestJobs.Add(job);
                    ddsInstrumentationContext.SaveChanges();
                    trans.Commit();
                }
                catch (Exception)
                {
                    trans.Rollback();
                    throw;
                }
            }
        }
    }
}
