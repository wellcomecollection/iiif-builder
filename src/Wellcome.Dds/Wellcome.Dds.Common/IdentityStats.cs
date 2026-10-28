using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wellcome.Dds.Common;

/// <summary>
/// An overall picture of the DDS identities table: how many records, of what kind, where they came
/// from, and how many have been validated or bound to a catalogue work. For the dashboard's
/// /Identity/Stats page.
/// </summary>
public class IdentityStats
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public int Total { get; set; }

    /// <summary>Rows by Level: package, volume, issue.</summary>
    public List<IdentityCount> ByLevel { get; set; } = new();

    // Package-level rows only, from here on, unless stated: the volume and issue rows copy their package's values.

    public int Packages { get; set; }
    public List<IdentityCount> BySource { get; set; } = new();
    public List<IdentityCount> ByStorageSpace { get; set; } = new();
    public List<IdentityCount> ByGenerator { get; set; } = new();

    /// <summary>Source and storage space together, e.g. "sierra / digitised", "calm / born-digital".</summary>
    public List<IdentityCount> BySourceAndStorageSpace { get; set; } = new();

    /// <summary>Packages whose record was created or refreshed by the generator (Goobi, Archivematica) processing them.</summary>
    public int PackagesFromGenerator { get; set; }

    /// <summary>Packages known only from parsing an identifier string: nothing has asserted they exist.</summary>
    public int PackagesParsedOnly { get; set; }

    public int PackagesStorageSpaceValidated { get; set; }
    public int PackagesSourceValidated { get; set; }
    public int PackagesWithCatalogueId { get; set; }

    /// <summary>Processed by a generator, so there should be a catalogue work, but none was found.</summary>
    public int PackagesFromGeneratorWithoutCatalogueId { get; set; }

    /// <summary>Packages that have volume or issue rows (multiple manifestations).</summary>
    public int PackagesWithChildren { get; set; }

    /// <summary>Rows (any level) whose Value is not already lowercase: the CALM refs, whose casing matters to storage.</summary>
    public int MixedCaseValues { get; set; }

    public DateTime? EarliestCreated { get; set; }
    public DateTime? LatestCreated { get; set; }
    public DateTime? LatestUpdated { get; set; }
    public int CreatedLast24Hours { get; set; }
    public int CreatedLast7Days { get; set; }
    public int CreatedLast30Days { get; set; }
    public int UpdatedLast24Hours { get; set; }
    public int UpdatedLast7Days { get; set; }

    public List<DdsIdentity> RecentlyCreatedPackages { get; set; } = new();
    public List<DdsIdentity> RecentlyUpdatedPackages { get; set; } = new();
    public List<DdsIdentity> RecentPackagesWithoutCatalogueId { get; set; } = new();
}

public class IdentityCount
{
    public string? Key { get; set; }
    public int Count { get; set; }
}

public interface IIdentityStatsRepository
{
    Task<IdentityStats> GetStats();
}
