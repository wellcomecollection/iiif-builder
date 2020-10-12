using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Wellcome.Dds.Repositories
{
    public class DdsContext : DbContext
    {
        public DdsContext(DbContextOptions<DdsContext> options) : base(options)
        { }

        public DbSet<Manifestation> Manifestations { get; set; }
        public DbSet<Metadata> Metadata { get; set; }

        public List<Manifestation> GetByAssetType(string type)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, int> GetTotalsByAssetType()
        {
            throw new NotImplementedException();
        }

        public List<Manifestation> AutoComplete(string query)
        {
            throw new NotImplementedException();
        }
    }
}
