using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.AssetDomain.DigitalObjects
{
    public class JobActivity
    {
        public List<Batch> BatchesForCurrentImages { get; set; }
        public List<Batch> BatchesForImagesRequiringSync { get; set; }
        public List<DlcsIngestJob> UpdatedJobs { get; set; }
    }
}
