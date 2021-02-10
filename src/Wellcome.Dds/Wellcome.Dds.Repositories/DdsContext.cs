using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using Utils;
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
            if (type == "(empty)")
            {
                type = null;
            }
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
            string pattern = null;
            if (Regex.IsMatch(query, "\\Ab\\d+\\z"))
            {
                pattern = $"%{query}%";
                return Manifestations.Where(m => 
                    m.Index == 0 &&
                    EF.Functions.ILike(query, m.PackageIdentifier, pattern))
                    .ToList();
            }
            if (query == "imfeelinglucky")
            {
                var sql = "SELECT * FROM manifestations OFFSET floor(random()*(select count(*) from manifestations)) LIMIT 1";
                return Manifestations.FromSqlRaw(sql).ToList();
            }

            pattern = $"%{query.ToAlphanumericOrWhitespace()}%";
            return Manifestations.Where(m => 
                m.Index == 0 &&
                EF.Functions.ILike(m.Label, pattern))
                .ToList();
        }
    }
    
    class AssetTotal
    {
        public const string Sql = "select asset_type, count(*) as asset_count from manifestations group by asset_type";
        public AssetTotal(DbDataReader dr)
        {
            if (dr.IsDBNull(0))
            {
                AssetType = "(empty)";
            }
            else
            {
                AssetType = (string) dr[0];
            }

            if (dr.IsDBNull(1))
            {
                AssetCount = -1;
            }
            else
            {
                AssetCount = (long) dr[1];
            }
        }

        public string AssetType { get; set; }
        public long AssetCount { get; set; }
    }
}
