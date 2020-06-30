using Microsoft.EntityFrameworkCore;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;

namespace Wellcome.Dds.AssetDomainRepositories.DlcsJobs
{
    public class CloudIngestContext : DbContext
    {
        public DbSet<DlcsIngestJob> DlcsIngestJobs { get; set; }
        public DbSet<DlcsBatch> DlcsBatches { get; set; }
        public DbSet<IngestAction> IngestActions { get; set; }
    }
}