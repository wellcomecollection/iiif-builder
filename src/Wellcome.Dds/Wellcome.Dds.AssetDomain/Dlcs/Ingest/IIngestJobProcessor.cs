using System;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.Dlcs.Ingest
{
    public interface IIngestJobProcessor
    {
        void ProcessQueue(int maxJobs = -1, bool usePriorityQueue = false, string filter = null);
        ImageIngestResult ProcessJob(DlcsIngestJob job, bool includeIngestingImages, bool forceReingest = false, bool usePriorityQueue = false);
        ImageIngestResult ProcessJob(DlcsIngestJob job, Func<Image, bool> includeIngestingImage, bool forceReingest = false, bool usePriorityQueue = false);

        void UpdateStatus();
    }
}
