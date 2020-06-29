using Microsoft.EntityFrameworkCore;

namespace Wellcome.Dds.Data
{
    public class DdsContext : DbContext
    {
        public DbSet<Manifestation> Manifestations { get; set; }
    }
}
