using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Utils;

namespace Wellcome.Dds.Common;

public class ParsingIdentityService(
    ILogger<ParsingIdentityService> logger,
    IMemoryCache memoryCache)
    : IIdentityService
{
    private const char Underscore = '_';
    private const char Slash = '/';
    private static readonly char[] Separators = [Underscore, Slash];
    
    public DdsIdentity GetIdentity(string s)
    {
        if (s.IsNullOrWhiteSpace())
        {
            throw new FormatException("Identifier has no content");
        }
        var lowered = s.ToLowerInvariant();
        // This lowered form is not the fully normalised form; we might cache under different keys
        if (memoryCache.TryGetValue(lowered, out DdsIdentity cachedIdentity))
        {
            return cachedIdentity;
        }

        var ddsIdentity = Parse(s);
        var possibleCacheKeys= GetCacheKeys(ddsIdentity, lowered);
        foreach(var key in possibleCacheKeys)
        {
            memoryCache.Set(key, ddsIdentity);
        }
        return ddsIdentity;
    }
    
    // public Task<DdsIdentity> GetIdentityAsync(string s)
    // {
    //     // use the same memory cache as above but with
    //     // var ddsIdentity = await ParseAsync(s);
    //     throw new NotImplementedException();
    // }

    private Task<DdsIdentity> ParseAsync(string s)
    {
        // This version will parse as below but ALSO consult some sort of lookup
        throw new NotImplementedException();
    }

    private static DdsIdentity Parse(string rawString)
    {
        var parts = rawString.Split(Separators);
        var hasBNumber = false;
        string source, value, pathElementSafe, packageIdentifier, packageIdentifierPathElementSafe;
        if (parts.Length > 0 && parts[0].IsBNumber())
        {
            // Explicitly throw if we see a bNumber/sequenceIndex format
            if (parts.Length == 2 && rawString[parts[0].Length] == '/')
            {
                throw new FormatException("BNumber / SequenceIndex format no longer supported.");
            }
            source = Source.Sierra; // For now
            hasBNumber = true;
            packageIdentifier = WellcomeLibraryIdentifiers.GetNormalisedBNumber(parts[0], true)!;
            value = rawString.ReplaceFirst(parts[0], packageIdentifier).ToLowerInvariant();
            packageIdentifierPathElementSafe = packageIdentifier;
            pathElementSafe = value; // e.g., b19974760_020_024
        }
        else
        {                
            // The supplied value was not a b number, or something that started with a b number.
            // Is there anything that would let us distinguish between a CALM ID and some other string?
            // For now, we can assume that the identifier might be born-digital, but we can no longer
            // dismiss is as invalid at this stage. We are assuming that anything that doesn't start with a 
            // BNumber is a potential born digital identifier.
            source = Source.Calm; // For now
            value = rawString.Replace(Underscore, Slash); // we can't normalise case for CALM identifiers
            packageIdentifier = value;
            packageIdentifierPathElementSafe = packageIdentifier.Replace(Slash, Underscore);
            pathElementSafe = packageIdentifierPathElementSafe;
        }
        var identity = new DdsIdentity
        {
            Value = value,
            PackageIdentifier = packageIdentifier,
            PackageIdentifierPathElementSafe = packageIdentifierPathElementSafe,
            PathElementSafe = pathElementSafe,
            Source = source,
            IsPackageLevelIdentifier = true
        };
        if (hasBNumber)
        {
            // These will no longer be true if some b numbers come through archivematica
            // ??? What about something parsed from a CALM ID that has a b number?
            // This class represents a particular identity and also gives alternatives
            // The result will be different if the starting string is different
            identity.StorageSpace = StorageSpace.Digitised;
            identity.Generator = Generator.Goobi; 
            
            if (parts.Length > 1 && value.StartsWith(packageIdentifier + Underscore))
            {
                identity.VolumePart = packageIdentifier + Underscore + parts[1];
                identity.IsPackageLevelIdentifier = false;
            }
            if (parts.Length > 2)
            {
                identity.IssuePart = packageIdentifier + Underscore + parts[1] + Underscore + parts[2];
            }
        }
        else
        {
            identity.StorageSpace = StorageSpace.BornDigital;
            identity.Generator = Generator.Archivematica; 
        }
            
        if (identity.PackageIdentifier.HasText() && identity.PackageIdentifierPathElementSafe.HasText() && identity.StorageSpace.HasText())
        {
            return identity;
        }

        throw new FormatException("Could not parse identifier");
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