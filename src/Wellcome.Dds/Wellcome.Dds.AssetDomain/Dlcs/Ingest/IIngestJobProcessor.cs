using System;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    public interface IIngestJobProcessor
    {
        Task ProcessQueue(int maxJobs = -1, bool usePriorityQueue = false, string filter = null);
        Task<ImageIngestResult> ProcessJob(DlcsIngestJob job, bool includeIngestingImages, bool forceReingest = false, bool usePriorityQueue = false);
        Task<ImageIngestResult> ProcessJob(DlcsIngestJob job, Func<Image, bool> includeIngestingImage, bool forceReingest = false, bool usePriorityQueue = false);

        void UpdateStatus();
    }
}
