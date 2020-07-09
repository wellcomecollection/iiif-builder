using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Wellcome.Dds.Data
{
    public class DdsContext : DbContext
    {
        public DbSet<Manifestation> Manifestations { get; set; }

        public List<Manifestation> GetByAssetType(string type)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, int> GetTotalsByAssetType()
        {
            throw new NotImplementedException();
        }

        public List<Manifestation> AutoComplete(string id)
        {
            throw new NotImplementedException();
        }
    }
}
