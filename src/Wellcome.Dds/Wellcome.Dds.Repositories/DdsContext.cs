using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using Utils.Database;

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
            return Manifestations.Where(m => m.AssetType == type)
                .Take(2000).ToList();
        }

        public Dictionary<string, long> GetTotalsByAssetType()
        {
            return Database
                .MapRawSql<AssetTotal>(AssetTotal.Sql, dr => new AssetTotal (dr))
                .Where(at => at.AssetType != null)
                .ToDictionary(at => at.AssetType, at => at.AssetCount);
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
    
    class AssetTotal
    {
        public const string Sql = "select asset_type, count(*) as asset_count from manifestations group by asset_type";
        public AssetTotal(DbDataReader dr)
        {
            AssetType = (string) dr[0];
            AssetCount = (long) dr[1];
        }

        public string AssetType { get; set; }
        public long AssetCount { get; set; }
    }
}
