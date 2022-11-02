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
                    EF.Functions.ILike(m.PackageIdentifier, pattern))
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
                EF.Functions.ILike(m.PackageLabel, pattern))
                .ToList();
        }

        public IEnumerable<AggregationMetadata> GetAggregation(string aggregator)
        {
            var query = String.Format(AggregationMetadata.Sql, aggregator.ToAlphanumeric());
            return Database
                .MapRawSql<AggregationMetadata>(query, dr => new AggregationMetadata(dr));
        }

        public IEnumerable<ValueAggregationResult> GetAggregation(string aggregator, string value)
        {
            var query =
                from metadata in Metadata
                join manifestation in Manifestations
                    on metadata.ManifestationId equals manifestation.PackageIdentifier
                where manifestation.Index == 0 && metadata.Label == aggregator && metadata.Identifier == value
                select new {metadata, manifestation};
            foreach (var result in query)
            {
                yield return new ValueAggregationResult
                {
                    Manifestation = result.manifestation,
                    CollectionStringValue = result.metadata.StringValue,
                    CollectionLabel = result.metadata.Label
                };
            }
        }

        public IEnumerable<ArchiveCollectionTop> GetTopLevelArchiveCollections()
        {
            return Database.MapRawSql(
                ArchiveCollectionTop.Sql, dr => new ArchiveCollectionTop(dr));
        }



        public Manifestation? GetManifestationByIndex(string identifier, int index)
        {
            return Manifestations.SingleOrDefault(
                m => m.PackageIdentifier == identifier && m.Index == index);
        }

        public Manifestation? GetManifestationByAnyIdentifier(string identifier)
        {
            return Manifestations.FirstOrDefault(mf => 
                mf.ManifestationIdentifier == identifier ||
                mf.WorkId == identifier ||
                mf.CalmRef == identifier ||
                mf.CalmAltRef == identifier);
        }
        
    }

    public record ValueAggregationResult
    {
        public Manifestation Manifestation;
        public string CollectionStringValue;
        public string CollectionLabel;
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

    public class AggregationMetadata
    {
        public const string Sql =
            "select distinct identifier, string_value from metadata where label='{0}' order by string_value";

        public readonly string Identifier;
        public readonly string Label;
        
        public AggregationMetadata(DbDataReader dr)
        {
            Identifier = (string) dr[0];
            Label = (string) dr[1];
        }
    }

    public class ArchiveCollectionTop
    {
        public const string Sql =
              "select distinct collection_reference_number, collection_title, " 
            + "collection_work_id from manifestations where collection_title is not null " 
            + "and collection_reference_number is not null";

        public readonly string ReferenceNumber;
        public readonly string Title;
        public readonly string WorkId;

        public ArchiveCollectionTop(DbDataReader dr)
        {
            ReferenceNumber = (string) dr[0];
            Title = (string) dr[1];
            if (!dr.IsDBNull(2))
            {
                WorkId = (string) dr[2];
            }
        }
    }
    
}
