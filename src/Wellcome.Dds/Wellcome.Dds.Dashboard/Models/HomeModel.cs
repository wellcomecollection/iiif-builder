using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.Dashboard.Models
{
    public class HomeModel
    {
        public JobsModel ProblemJobs { get; set; }
        public Page<ErrorByMetadata> ErrorsByMetadataPage { get; set; }
        public Dictionary<string, IngestAction> IngestActions { get; set; }
    }
}
