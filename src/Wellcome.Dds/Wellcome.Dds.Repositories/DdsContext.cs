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
            //     const string BNumberSql =
            //         "SELECT TOP 100 * FROM FlatManifestations where Manifestation=0 AND PackageIdentifier LIKE @query";
            //     const string TitleSql =
            //         "SELECT TOP 100 * FROM FlatManifestations where Manifestation=0 AND RootSectionTitle LIKE @query";
            //     const string ImFeelingLuckySql =
            //         "SELECT top 1 * from FlatManifestations where AssetType='seadragon/dzi' and Manifestation=0 order by NEWID()";
            //
            //     if (query == "imfeelinglucky")
            //     {
            //         return Database.SqlQuery<FlatManifestation>(ImFeelingLuckySql).ToList();
            //     }
            //
            //     string sql;
            //     SqlParameter queryParam = new SqlParameter("@query", SqlDbType.NVarChar);
            //     if (Regex.IsMatch(query, "\\Ab\\d+\\z"))
            //     {
            //         queryParam.Value = string.Format("{0}%", query);
            //         sql = BNumberSql;
            //     }
            //     else
            //     {
            //         queryParam.Value = string.Format("%{0}%", query);
            //         sql = TitleSql;
            //     }
            //     return Database.SqlQuery<FlatManifestation>(sql, queryParam).ToList();
            // }
        }
    }
}
