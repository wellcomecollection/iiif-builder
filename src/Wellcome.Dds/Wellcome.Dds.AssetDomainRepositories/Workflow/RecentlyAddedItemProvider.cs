using System;
using System.Collections.Generic;
using System.Linq;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Workflow
{
    public class RecentlyAddedItemProvider : IDatedIdentifierProvider
    {
        private DdsInstrumentationContext ddsInstrumentationContext;

        public RecentlyAddedItemProvider(DdsInstrumentationContext ddsInstrumentationContext)
        {
            this.ddsInstrumentationContext = ddsInstrumentationContext;
        }

        public List<DatedIdentifier> GetDatedIdentifiers(DateTime @from, DateTime? to)
        {
            if (!to.HasValue) to = DateTime.Now;
            int max = 1000;
            return GetRecentJobsAsDatedIdentifiers(from, to.Value, max);
        }

        public List<DatedIdentifier> GetDatedIdentifiers(int count)
        {
            var now = DateTime.Now;
            return GetRecentJobsAsDatedIdentifiers(now.AddYears(-1), now, count);
        }


        // TODO - remove this, just left here to show what was going on before for testing
//        const string JobSql = @"
//with cte as (
//    select distinct Identifier, Min(Created) As FirstSeen 
//    from DlcsIngestJobs GROUP BY Identifier
//)
//select top (@pNumber) Identifier, FirstSeen from cte 
//where FirstSeen >= @pFrom and FirstSeen<@pTo  order by FirstSeen desc
//";

//        const string FmSql = @"
//Declare @IdTable Table (id varchar(10))
//insert @IdTable(id) values $values$;

//select PackageIdentifier, RootSectionTitle
//FROM [Dds].[dbo].[FlatManifestations]
//where PackageIdentifier in (select id from @IdTable) and Manifestation=0
//";

        private List<DatedIdentifier> GetRecentJobsAsDatedIdentifiers(DateTime from, DateTime to, int max)
        {
            if (max > 1000) max = 1000;

            return ddsInstrumentationContext
                .DlcsIngestJobs
                .GroupBy(j => new { j.Identifier, j.Label })
                .Select(g => new DatedIdentifier
                {
                    Identifier = g.Key.Identifier,
                    Label = g.Key.Label,
                    Date = g.Min(x => x.Created)
                })
                .Distinct()
                .Where(j => j.Date >= from && j.Date < to)
                .OrderByDescending(j => j.Date)
                .Take(max)
                .ToList();
        }

    }

}
