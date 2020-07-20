using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    public interface IIngestJobRegistry
    {
        /// <summary>
        /// Async registering of job to DB.
        /// Will be picked up by Queue.
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="useInitialOrigin"></param>
        /// <returns>List of job IDs</returns>
        Task<DlcsIngestJob[]> RegisterImages(string identifier, bool useInitialOrigin = false);

        /// <summary>
        /// Registers a job or jobs (for MMs) with the "startProcessed" already set, so that
        /// it will not be picked up by the job processor.
        /// 
        /// This is use when the caller intends to trigger the processing of the job, 
        /// with the expectation that they will do it fairly soon after calling. 
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        IAsyncEnumerable<DlcsIngestJob> RegisterImagesForImmediateStartAsync(string identifier);

        IEnumerable<DlcsIngestJob> GetJobs(int number);
        IEnumerable<DlcsIngestJob> GetQueue(DateTime? after);
        IEnumerable<DlcsIngestJob> GetProblems(int maxToFetch);
        DlcsIngestJob GetJob(int id);
    }
}