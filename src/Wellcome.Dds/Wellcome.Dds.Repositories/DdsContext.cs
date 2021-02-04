using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
            if (Regex.IsMatch(query, "\\Ab\\d+\\z"))
            {
                return Manifestations.Where(m => 
                    m.Index == 0 &&
                    m.PackageIdentifier.Contains(query)).ToList();
            }
            if (query == "imfeelinglucky")
            {
                var sql = "SELECT * FROM manifestations OFFSET floor(random()*(select count(*) from manifestations)) LIMIT 1";
                return Manifestations.FromSqlRaw(sql).ToList();
            }
            return Manifestations.Where(m => 
                m.Index == 0 &&
                m.Label.Contains(query)).ToList();
        }
    }
}
