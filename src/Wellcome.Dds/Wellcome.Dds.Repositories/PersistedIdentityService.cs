using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Utils;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories;

public class PersistedIdentityService(
    ILogger<PersistedIdentityService> logger,
    IMemoryCache memoryCache,
    DdsContext ddsContext,
    StorageServiceClient storageServiceClient,
    ICatalogue catalogue) : IIdentityService
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
        
        // Now try the database
        var dbIdentity = ddsContext.Identities.Find(lowered);
        if (dbIdentity != null)
        {
            if (CanReturnStoredIdentity(dbIdentity, generator))
            {
                CacheIdentity(dbIdentity, lowered);
                return dbIdentity;
            }
        }

        // We either need to create a new Identity record, or we need to update its generator.
        // Either way, the generator must be one of our known set.
        if (generator.HasText() && !Generator.IsKnown(generator))
        {
            if (!Generator.IsIgnored(generator))
            {
                throw new InvalidEnumArgumentException($"Generator '{generator}' is unknown");
            }
        }

        if (dbIdentity != null && generator.IsNullOrWhiteSpace())
        {
            // At this point, dbIdentity must be null,
            // if no generator supplied we should have returned dbIdentity already.
            throw new InvalidOperationException("Unhandled DBIdentity");
        }

        bool isNew = false;
        if (dbIdentity == null)
        {
            // Going to lean on our existing parsing implementation, and then overrule it with more knowledge.
            // The parsed Identity embodies our default assumptions.
            dbIdentity = ParsingIdentityService.Parse(s);
            isNew = true;
            ddsContext.Identities.Add(dbIdentity);
        }

        var updateVolumesAndIssues = false;
        if (dbIdentity.IsPackageLevelIdentifier)
        {
            if (generator.HasText())
            {
                logger.LogInformation("{operation} authoritative package level record for {packageIdentifier} from {generator}",
                    isNew ? "Creating" : "Updating", dbIdentity.PackageIdentifier, generator);
                dbIdentity.Generator = generator;
                ValidateStorageSpace(dbIdentity);
                ValidateSource(dbIdentity);
                SetCatalogueId(dbIdentity);
                dbIdentity.FromGenerator = true;
                dbIdentity.Updated = DateTime.UtcNow;
                updateVolumesAndIssues = true;
            }
        }
        else
        {
            // The requested identity was a volume or issue, not a package
            if (generator.HasText())
            {
                throw new InvalidEnumArgumentException(
                    $"Generator '{generator}' can only be asserted on package level identifiers");
            }
            var packageIdentity = ddsContext.Identities.Find(dbIdentity.PackageIdentifier.ToLowerInvariant());
            if (packageIdentity == null)
            {
                // The package level identifier doesn't exist yet.
                // Should we create it? Its values will be provisional, only from parsing, until this code is called
                // for the package level identifier with a generator.
                logger.LogInformation("Volume or issue identity {value} resolved where no package-level identity exists",
                    dbIdentity.Value);
                // For anything existing this will only apply to Goobi/digitised/Sierra, which is what we parse.
                // Leave dbIdentity with its parsed 
            }
            else
            {
                // update dbIdentity with the latest values from the PackageIdentifier
                dbIdentity.Generator = packageIdentity.Generator;
                dbIdentity.StorageSpace = packageIdentity.StorageSpace;
                dbIdentity.Source = packageIdentity.Source;
                dbIdentity.FromGenerator = packageIdentity.FromGenerator;
                dbIdentity.SourceValidated = packageIdentity.SourceValidated;
                dbIdentity.StorageSpaceValidated = packageIdentity.StorageSpaceValidated;
                dbIdentity.CatalogueId = packageIdentity.CatalogueId;
                dbIdentity.Updated = DateTime.UtcNow;
            }
        }
        
        ddsContext.SaveChanges();
        CacheIdentity(dbIdentity, lowered);
        
        if (updateVolumesAndIssues)
        {
            var rows = ddsContext.Database.ExecuteSqlInterpolated(
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
            logger.LogInformation("Updated {rows} volume and issue rows for {packageIdentifier}", 
                rows, dbIdentity.PackageIdentifier);
        }
        
        return dbIdentity;
    }

    private void CacheIdentity(DdsIdentity dbIdentity, string lowered)
    {
        var possibleCacheKeys= GetCacheKeys(dbIdentity, lowered);
        foreach(var key in possibleCacheKeys)
        {
            memoryCache.Set(key, dbIdentity);
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
                        identity.PackageIdentifier, identity.StorageSpace);
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