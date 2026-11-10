using System;
using FluentAssertions;
using Wellcome.Dds.Identifiers;
using Xunit;

namespace Wellcome.Dds.Tests.Identifiers;

public class IdentifierSetTests
{
    private static SourceIdentifier Row(string sourceSystem, string value, bool isAlias, int daysAgo)
        => new SourceIdentifier
        {
            Type = OntologyType.Work,
            SourceSystem = sourceSystem,
            Value = value,
            IsAlias = isAlias,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo)
        };

    [Fact]
    public void Original_Is_The_Non_Alias_Row()
    {
        var set = new IdentifierSet
        {
            CanonicalId = "vsyg427x",
            SourceIdentifiers = new[]
            {
                Row(SourceSystems.AxiellGuid, "3c5b8da0-6237-4e98-848a-ad624b99d17c", true, 1),
                Row(SourceSystems.CalmRecordId, "c460f493-294e-4043-87bc-2aa753c35f95", false, 100)
            }
        };

        set.Original.Should().NotBeNull();
        set.Original.SourceSystem.Should().Be(SourceSystems.CalmRecordId);
        set.PreservationIdentifier.Should().BeNull("there is no mets row in this set");
    }

    [Fact]
    public void Preservation_Identifier_Is_The_Mets_Row()
    {
        var set = new IdentifierSet
        {
            CanonicalId = "q8fhz52m",
            SourceIdentifiers = new[] { Row(SourceSystems.Mets, "artcoo/b/15", false, 100) }
        };

        set.PreservationIdentifier.Should().NotBeNull();
        set.PreservationIdentifier.Value.Should().Be("artcoo/b/15");
    }

    [Fact]
    public void Resolution_Surfaces_Distinct_Preservation_Identifiers()
    {
        var resolution = new IdentifierResolution("ARTCOO/B/15");
        resolution.Lookups.Add(new CandidateLookup(
            new IdentifierCandidate("ARTCOO/B/15", SourceSystems.Mets, OntologyType.Work, "test"), "url1")
        {
            IdentifierSet = new IdentifierSet
            {
                CanonicalId = "q8fhz52m",
                SourceIdentifiers = new[] { Row(SourceSystems.Mets, "artcoo/b/15", false, 100) }
            }
        });
        resolution.Lookups.Add(new CandidateLookup(
            new IdentifierCandidate("ARTCOO/B/15", SourceSystems.CalmRefNo, OntologyType.Work, "test"), "url2")
        {
            IdentifierSet = new IdentifierSet
            {
                CanonicalId = "m2fe2wrv",
                SourceIdentifiers = new[] { Row(SourceSystems.CalmRefNo, "ARTCOO/B/15", false, 100) }
            }
        });
        resolution.Lookups.Add(new CandidateLookup(
            new IdentifierCandidate("ARTCOO/B/15", SourceSystems.CalmAltRefNo, OntologyType.Work, "test"), "url3"));

        resolution.AnyFound.Should().BeTrue();
        var preservation = resolution.PreservationIdentifierLookups.Should().ContainSingle().Which;
        preservation.IdentifierSet.CanonicalId.Should().Be("q8fhz52m");
        preservation.IdentifierSet.PreservationIdentifier.Value.Should().Be("artcoo/b/15");
    }
}
