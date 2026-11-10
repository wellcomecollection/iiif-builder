using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories;

/// <summary>
/// Counts over the identities table for the dashboard. Each figure is one aggregate query; nothing is
/// loaded into memory except the three short recent lists.
/// </summary>
public class IdentityStatsRepository : IIdentityStatsRepository
{
    private const int RecentListSize = 10;

    private readonly DdsContext ddsContext;

    public IdentityStatsRepository(DdsContext ddsContext)
    {
        this.ddsContext = ddsContext;
    }

    public async Task<IdentityStats> GetStats()
    {
        var all = ddsContext.Identities.AsNoTracking();
        var packages = all.Where(i => i.IsPackageLevelIdentifier);
        var now = DateTime.UtcNow;
        var stats = new IdentityStats { GeneratedAt = now };

        stats.Total = await all.CountAsync();
        stats.ByLevel = await CountBy(all, i => i.Level);

        stats.Packages = await packages.CountAsync();
        stats.BySource = await CountBy(packages, i => i.Source);
        stats.ByStorageSpace = await CountBy(packages, i => i.StorageSpace);
        stats.ByGenerator = await CountBy(packages, i => i.Generator);
        stats.BySourceAndStorageSpace = (await packages
                .GroupBy(i => new { i.Source, i.StorageSpace })
                .Select(g => new { g.Key.Source, g.Key.StorageSpace, Count = g.Count() })
                .ToListAsync())
            .Select(g => new IdentityCount { Key = $"{g.Source} / {g.StorageSpace ?? "(none)"}", Count = g.Count })
            .OrderByDescending(c => c.Count)
            .ToList();

        stats.PackagesFromGenerator = await packages.CountAsync(i => i.FromGenerator);
        stats.PackagesParsedOnly = stats.Packages - stats.PackagesFromGenerator;
        stats.PackagesStorageSpaceValidated = await packages.CountAsync(i => i.StorageSpaceValidated);
        stats.PackagesSourceValidated = await packages.CountAsync(i => i.SourceValidated);
        stats.PackagesWithCatalogueId = await packages.CountAsync(i => i.CatalogueId != null && i.CatalogueId != "");
        stats.PackagesFromGeneratorWithoutCatalogueId = await packages.CountAsync(i => i.FromGenerator && (i.CatalogueId == null || i.CatalogueId == ""));
        stats.PackagesWithChildren = await all.Where(i => !i.IsPackageLevelIdentifier).Select(i => i.PackageIdentifier).Distinct().CountAsync();
        stats.MixedCaseValues = await all.CountAsync(i => i.Value != i.LowerCaseValue);

        stats.EarliestCreated = await all.MinAsync(i => (DateTime?) i.Created);
        stats.LatestCreated = await all.MaxAsync(i => (DateTime?) i.Created);
        stats.LatestUpdated = await all.MaxAsync(i => (DateTime?) i.Updated);
        stats.CreatedLast24Hours = await all.CountAsync(i => i.Created > now.AddHours(-24));
        stats.CreatedLast7Days = await all.CountAsync(i => i.Created > now.AddDays(-7));
        stats.CreatedLast30Days = await all.CountAsync(i => i.Created > now.AddDays(-30));
        stats.UpdatedLast24Hours = await all.CountAsync(i => i.Updated > now.AddHours(-24));
        stats.UpdatedLast7Days = await all.CountAsync(i => i.Updated > now.AddDays(-7));

        stats.RecentlyCreatedPackages = await packages.OrderByDescending(i => i.Created).Take(RecentListSize).ToListAsync();
        stats.RecentlyUpdatedPackages = await packages.OrderByDescending(i => i.Updated).Take(RecentListSize).ToListAsync();
        stats.RecentPackagesWithoutCatalogueId = await packages
            .Where(i => i.FromGenerator && (i.CatalogueId == null || i.CatalogueId == ""))
            .OrderByDescending(i => i.Updated)
            .Take(RecentListSize)
            .ToListAsync();

        return stats;
    }

    private static async Task<List<IdentityCount>> CountBy(IQueryable<DdsIdentity> rows, System.Linq.Expressions.Expression<Func<DdsIdentity, string?>> key)
    {
        var counts = await rows
            .GroupBy(key)
            .Select(g => new IdentityCount { Key = g.Key, Count = g.Count() })
            .ToListAsync();
        return counts.OrderByDescending(c => c.Count).ToList();
    }
}
