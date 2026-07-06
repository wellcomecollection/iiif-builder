using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories;

public class PersistedIdentityService(
    ILogger<PersistedIdentityService> logger,
    IMemoryCache memoryCache,
    IServiceScopeFactory scopeFactory,
    StorageServiceClient storageServiceClient,
    ICatalogue catalogue,
    IOptions<DdsOptions> ddsOptions) : IIdentityService
{
    public DdsIdentity GetIdentity(string s)
    {
        return GetIdentity(s, null);
    }

    private bool CanReturnStoredIdentity(DdsIdentity identity, string? incomingGenerator)
    {
        if (incomingGenerator.HasText() && Generator.IsIgnored(incomingGenerator))
        {
            return true;
        }
        if (incomingGenerator.HasText() && !identity.FromGenerator)
        {
            // We are being given new authoritative information
            return false;
        }
        if (incomingGenerator.IsNullOrWhiteSpace() || incomingGenerator == identity.Generator)
        {
            // No change in generator, can return stored version
            return true;
        }
        return false;
    }

    public DdsIdentity GetIdentity(string s, string? generator)
    {        
        if (s.IsNullOrWhiteSpace())
        {
            throw new FormatException("Identifier has no content");
        }
        var lowered = s.Trim().ToLowerInvariant();
        
        // I'm not going to generally unencode this, but it's worth catching any unintended `/` encoding:
        if (lowered.Contains("%2f"))
        {
            s = s.Replace("%2f", "/",  StringComparison.InvariantCultureIgnoreCase);
            lowered = s.Trim().ToLowerInvariant();
        }
        
        // This lowered form is not the fully normalised form; we might cache under different keys
        
        // First try the MemoryCache
        if (memoryCache.TryGetValue(lowered, out DdsIdentity cachedIdentity))
        {
            if (CanReturnStoredIdentity(cachedIdentity, generator))
            {
                return cachedIdentity;
            }
        }
        
        // Parse up front to obtain the CANONICAL key. The stored primary key (LowerCaseValue) is the
        // normalised form, NOT the raw input (e.g. CALM "PPCRI_A_1" normalises to "ppcri/a/1", and a
        // check-digit-less b-number gains its check digit). Looking up by the raw input misses the
        // existing row and previously caused a duplicate-key insert, so we key on the canonical form.
        var parsed = ParsingIdentityService.Parse(s);
        var canonicalKey = parsed.LowerCaseValue;

        // The canonical form may differ from the raw input; try the cache again under it.
        if (canonicalKey != lowered
            && memoryCache.TryGetValue(canonicalKey, out DdsIdentity canonicalCached))
        {
            if (CanReturnStoredIdentity(canonicalCached, generator))
            {
                return canonicalCached;
            }
        }

        // A DbContext is not thread-safe, and this service is called concurrently (e.g. the recursive
        // Task.WhenAll manifestation fan-out in DigitalObjectRepository). Resolve against a short-lived
        // context of our own rather than the caller's shared scoped context. This is only reached on a
        // cache miss - cache hits above return without creating a scope or touching the database.
        using var scope = scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DdsContext>();

        // Now the database, keyed by the canonical value.
        var dbIdentity = ctx.Identities.Find(canonicalKey);
        if (dbIdentity != null)
        {
            if (CanReturnStoredIdentity(dbIdentity, generator))
            {
                CacheIdentity(dbIdentity, lowered, provisional: false);
                return dbIdentity;
            }
        }

        // READ PATH: no authoritative generator supplied (or an ignored one, e.g. dashboard, which is
        // explicitly non-authoritative). Return the stored record if we have one, otherwise the parsed
        // identity, enriched from its package-level record for volumes/issues. Reads never create
        // records: this service can't know whether a volume or issue really exists, and any requested
        // URL can name one that doesn't, so persisting here would let arbitrary requests fill the
        // table. Volume/issue rows are created only via RegisterAuthoritativeChild, whose callers
        // have enumerated the child from the package's METS.
        if (generator.IsNullOrWhiteSpace() || Generator.IsIgnored(generator))
        {
            if (dbIdentity != null)
            {
                CacheIdentity(dbIdentity, lowered, provisional: !dbIdentity.FromGenerator);
                return dbIdentity;
            }
            if (!parsed.IsPackageLevelIdentifier)
            {
                EnrichFromPackage(parsed, ctx);
            }
            CacheIdentity(parsed, lowered, provisional: !parsed.FromGenerator);
            return parsed;
        }

        // WRITE PATH: an authoritative generator was supplied. Create or update the record.
        // The generator must be one of our known set (ignored generators, e.g. dashboard, are handled
        // by the CanReturnStoredIdentity rules above when a record already exists).
        if (!Generator.IsKnown(generator) && !Generator.IsIgnored(generator))
        {
            throw new InvalidEnumArgumentException($"Generator '{generator}' is unknown");
        }

        // A generator can only be asserted on package-level identifiers.
        if (!parsed.IsPackageLevelIdentifier)
        {
            throw new InvalidEnumArgumentException(
                $"Generator '{generator}' can only be asserted on package level identifiers");
        }

        var isNew = false;
        if (dbIdentity == null)
        {
            // Lean on the parsed identity for our default assumptions, then overrule with more knowledge.
            dbIdentity = parsed;
            isNew = true;
            ctx.Identities.Add(dbIdentity);
        }

        logger.LogInformation("{operation} authoritative package level record for {packageIdentifier} from {generator}",
            isNew ? "Creating" : "Updating", dbIdentity.PackageIdentifier, generator);
        dbIdentity.Generator = generator;
        ValidateStorageSpace(dbIdentity);
        ValidateSource(dbIdentity);
        SetCatalogueId(dbIdentity);
        dbIdentity.FromGenerator = true;
        dbIdentity.Updated = DateTime.UtcNow;

        // Persist the package row and propagate the authoritative values to its volume/issue rows in a
        // single transaction, so a partial failure can't leave the children out of step with the package.
        int rows;
        using (var tx = ctx.Database.BeginTransaction())
        {
            ctx.SaveChanges();
            rows = ctx.Database.ExecuteSqlInterpolated(
                $"""
                 UPDATE identities set 
                 generator={dbIdentity.Generator}, 
                 storage_space={dbIdentity.StorageSpace}, 
                 source={dbIdentity.Source}, 
                 from_generator={dbIdentity.FromGenerator}, 
                 source_validated={dbIdentity.SourceValidated}, 
                 storage_space_validated={dbIdentity.StorageSpaceValidated},
                 catalogue_id={dbIdentity.CatalogueId},
                 updated={dbIdentity.Updated} 
                 WHERE package_identifier={dbIdentity.PackageIdentifier} AND NOT is_package_level_identifier
                 """);
            tx.Commit();
        }
        logger.LogInformation("Updated {rows} volume and issue rows for {packageIdentifier}",
            rows, dbIdentity.PackageIdentifier);

        CacheIdentity(dbIdentity, lowered, provisional: false);
        // The bulk UPDATE bypassed EF and the cache; drop any cached (provisional) child entries so
        // the next read re-resolves them with the new authoritative values.
        InvalidateChildCache(ctx, dbIdentity.PackageIdentifier);

        return dbIdentity;
    }

    public DdsIdentity RegisterAuthoritativeChild(string s)
    {
        // The caller is asserting that this volume/issue really exists - it has been enumerated
        // from its package's METS. This is the only path that persists child identities; plain
        // reads never do. Volumes/issues only exist for b numbers, which are fully normalised,
        // so this carries no CALM case risk.
        var identity = GetIdentity(s);
        if (identity.IsPackageLevelIdentifier || !identity.FromGenerator)
        {
            // Not a child, or its package is not (yet) authoritative: nothing worth persisting.
            return identity;
        }

        using var scope = scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DdsContext>();
        if (ctx.Identities.Find(identity.LowerCaseValue) != null)
        {
            return identity;
        }
        ctx.Identities.Add(identity);
        try
        {
            ctx.SaveChanges();
            logger.LogInformation("Persisted volume/issue identity {value} for package {packageIdentifier}",
                identity.Value.LogSafe(), identity.PackageIdentifier);
        }
        catch (DbUpdateException)
        {
            // A concurrent request inserted the same child row first; it holds the same values.
        }
        return identity;
    }

    private void EnrichFromPackage(DdsIdentity identity, DdsContext ctx)
    {
        // Inherit authoritative values from the package-level record if one exists, WITHOUT persisting
        // anything (reads must not write). Only relevant for volume/issue identifiers.
        var packageKey = identity.PackageIdentifier.ToLowerInvariant();
        var package = memoryCache.TryGetValue(packageKey, out DdsIdentity cachedPackage)
            ? cachedPackage
            : ctx.Identities.Find(packageKey);
        if (package == null)
        {
            // The package-level identifier doesn't exist yet; leave the provisional parsed values.
            // For anything existing this only applies to Goobi/digitised/Sierra, which is what we parse.
            logger.LogInformation("Volume or issue identity {value} resolved where no package-level identity exists",
                identity.Value.LogSafe());
            return;
        }
        identity.Generator = package.Generator;
        identity.StorageSpace = package.StorageSpace;
        identity.Source = package.Source;
        identity.FromGenerator = package.FromGenerator;
        identity.SourceValidated = package.SourceValidated;
        identity.StorageSpaceValidated = package.StorageSpaceValidated;
        identity.CatalogueId = package.CatalogueId;
    }

    private void CacheIdentity(DdsIdentity dbIdentity, string lowered, bool provisional)
    {
        // Provisional (parse-only) entries get a shorter lifetime than authoritative ones, so a record
        // that is later confirmed by a generator on another instance is picked up sooner. Expiry also
        // bounds the cache size (there is no global SizeLimit - the cache is shared with other users).
        var minutes = provisional
            ? ddsOptions.Value.ProvisionalIdentityCacheMinutes
            : ddsOptions.Value.IdentityCacheMinutes;
        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes)
        };
        var possibleCacheKeys= GetCacheKeys(dbIdentity, lowered);
        foreach(var key in possibleCacheKeys)
        {
            memoryCache.Set(key, dbIdentity, entryOptions);
        }
    }

    private void InvalidateChildCache(DdsContext ctx, string packageIdentifier)
    {
        // Evict any cached volume/issue entries for this package so a subsequent read re-resolves them
        // from the just-updated database rows rather than serving a stale provisional copy.
        var children = ctx.Identities
            .Where(i => i.PackageIdentifier == packageIdentifier && !i.IsPackageLevelIdentifier)
            .ToList();
        foreach (var child in children)
        {
            foreach (var key in GetCacheKeys(child, child.LowerCaseValue))
            {
                memoryCache.Remove(key);
            }
        }
    }

    private void SetCatalogueId(DdsIdentity identity)
    {
        try
        {
            var work = catalogue.GetWorkByOtherIdentifier(identity.PackageIdentifier).Result;
            if (work != null && work.Id.HasText())
            {
                if (identity.CatalogueId.HasText() && identity.CatalogueId != work.Id)
                {
                    logger.LogWarning("Change of Catalogue ID for {packageIdentifier}. Was {oldCatalogueId}, is now {newCatalogueId}", 
                        identity.PackageIdentifier, identity.CatalogueId, work.Id);
                }
                identity.CatalogueId = work.Id;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to set catalogue id for package {packageIdentifier}", identity.PackageIdentifier);
        }
    }

    private void ValidateStorageSpace(DdsIdentity identity)
    {
        // try the expected one first (currently we only have two so this is a bit overkill but will work with more)
        if (StorageSpace.IsKnown(identity.StorageSpace))
        {
            try
            {
                _ = storageServiceClient.LoadStorageManifest(identity.StorageSpace!, identity.PackageIdentifier).Result;
                identity.StorageSpaceValidated = true;
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Storage space for package {packageIdentifier} is not the parsed {storageSpace}: " + ex.Message, 
                    identity.PackageIdentifier, identity.StorageSpace);
            }
        }
        // try the others (there will be only one)
        foreach (var storageSpace in StorageSpace.All)
        {
            if (storageSpace != identity.StorageSpace) // we already tried that first
            {
                try
                {
                    _ = storageServiceClient.LoadStorageManifest(storageSpace!, identity.PackageIdentifier).Result;
                    identity.StorageSpace = storageSpace;
                    identity.StorageSpaceValidated = true;
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Storage space for package {packageIdentifier} is not the attempted {storageSpace}: " + ex.Message,
                        identity.PackageIdentifier, storageSpace);
                }
            }
        }
        
        identity.StorageSpaceValidated = false;
    }

    private void ValidateSource(DdsIdentity identity)
    {
        // Whether the catalogue API source of truth is Sierra or Calm
        // TODO: look up original catalogue source. 
        // Come back to this when we need it. For now the parsed answer is OK.

        identity.SourceValidated = false;
    }


    private List<string> GetCacheKeys(DdsIdentity ddsIdentity, string originalString)
    {
        // In a lookup implementation, this could include the catalogue API 
        // It returns the various normalised forms.
        var keys = new List<string> { ddsIdentity.Value.ToLowerInvariant() };
        var pathLower = ddsIdentity.PathElementSafe.ToLowerInvariant();
        if (pathLower != keys[0])
        {
            keys.Add(pathLower);
        }

        if (!keys.Contains(originalString))
        {
            keys.Add(originalString);
        }
        return keys;
    }
}