using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wellcome.Dds.Common;

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
        Task<DlcsIngestJob[]> RegisterImages(DdsIdentifier identifier, bool useInitialOrigin = false);

        /// <summary>
        /// Registers a job or jobs (for MMs) with the "startProcessed" already set, so that
        /// it will not be picked up by the job processor.
        /// 
        /// This is use when the caller intends to trigger the processing of the job, 
        /// with the expectation that they will do it fairly soon after calling. 
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        IAsyncEnumerable<DlcsIngestJob> RegisterImagesForImmediateStart(DdsIdentifier identifier);

        Task<IEnumerable<DlcsIngestJob>> GetRecentJobs(int number);
        
        /// <summary>
        /// Get all jobs that have yet to be started.
        /// </summary>
        Task<IEnumerable<DlcsIngestJob>> GetQueue(DateTime? after);
        
        /// <summary>
        /// Get most recent jobs that have not succeeded or have ErrorText
        /// </summary>
        Task<IEnumerable<DlcsIngestJob>> GetProblems(int maxToFetch);
        
        Task<DlcsIngestJob> GetJob(int id);
    }
}