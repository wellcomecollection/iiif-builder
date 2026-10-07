using System.Linq;
using FluentAssertions;
using Wellcome.Dds.Identifiers;
using Xunit;

namespace Wellcome.Dds.Tests.Identifiers;

public class IdentifierCandidatesTests
{
    [Theory]
    [InlineData("vsyg427x")]
    [InlineData("nydjbrr7")]
    [InlineData("a2345bcd")]
    public void Canonical_Id_Shape_Gives_Forward_Lookup_Only(string id)
    {
        var candidates = IdentifierCandidates.GetCandidates(id);

        candidates.Should().ContainSingle();
        candidates[0].IsCanonical.Should().BeTrue();
        candidates[0].Value.Should().Be(id);
    }

    [Theory]
    [InlineData("b14658197")]
    [InlineData("b1465819")]
    [InlineData("b1465819a")]
    [InlineData(" b14658197 ")]
    public void B_Number_Is_Normalised_And_Tried_Under_Sierra_And_Mets(string id)
    {
        var candidates = IdentifierCandidates.GetCandidates(id);

        candidates.Select(c => c.SourceSystem).Should()
            .Equal(SourceSystems.Mets, SourceSystems.SierraSystemNumber, SourceSystems.SierraIdentifier);
        candidates.Take(2).Should().OnlyContain(c => c.Value == "b14658197" && c.Type == OntologyType.Work);
        candidates[2].Value.Should().Be("1465819");
    }

    [Fact]
    public void B_Number_That_Also_Matches_Canonical_Pattern_Gets_Both()
    {
        // 8 chars, starts with a letter, no o/i/l/1 - but also b + 7 digits
        var candidates = IdentifierCandidates.GetCandidates("b2345678");

        candidates.Should().Contain(c => c.IsCanonical && c.Value == "b2345678");
        candidates.Should().Contain(c => c.SourceSystem == SourceSystems.SierraSystemNumber && c.Value == "b23456784");
    }

    [Theory]
    [InlineData("c460f493-294e-4043-87bc-2aa753c35f95")]
    [InlineData("C460F493-294E-4043-87BC-2AA753C35F95")]
    [InlineData("{c460f493-294e-4043-87bc-2aa753c35f95}")]
    public void Guid_Is_Tried_Under_Calm_Axiell_And_Folio(string id)
    {
        var candidates = IdentifierCandidates.GetCandidates(id);

        candidates.Select(c => c.SourceSystem).Should()
            .Equal(SourceSystems.CalmRecordId, SourceSystems.AxiellGuid, SourceSystems.FolioInstance);
        candidates.Should().OnlyContain(c => c.Value == "c460f493-294e-4043-87bc-2aa753c35f95");
    }

    [Theory]
    [InlineData("in00012345")]
    [InlineData("IN00012345")]
    public void Folio_Instance_Hrid_Is_Tried_Under_Folio_Instance_Hrid(string id)
    {
        var candidates = IdentifierCandidates.GetCandidates(id);

        candidates.Should().ContainSingle();
        candidates[0].SourceSystem.Should().Be(SourceSystems.FolioInstanceHrid);
        candidates[0].Value.Should().Be(id);
        candidates[0].Type.Should().Be(OntologyType.Work);
    }

    [Theory]
    [InlineData("V0012345")]
    [InlineData("L0012345EB")]
    public void Miro_Number_Is_Tried_As_Work_And_Image(string id)
    {
        var candidates = IdentifierCandidates.GetCandidates(id);

        candidates.Should().HaveCount(2);
        candidates.Should().OnlyContain(c => c.SourceSystem == SourceSystems.MiroImageNumber && c.Value == id);
        candidates.Select(c => c.Type).Should().Equal(OntologyType.Work, OntologyType.Image);
    }

    [Theory]
    [InlineData("ARTCOO/B/15", "ARTCOO/B/15")]
    [InlineData("ARTCOO_B_15", "ARTCOO/B/15")]
    [InlineData("PPCRI/A/B/C", "PPCRI/A/B/C")]
    [InlineData("MS.1234", "MS.1234")]
    public void Anything_Else_Is_Tried_As_A_Preservation_Id_And_Calm_Reference(string id, string expectedValue)
    {
        // ARTCOO/B/15 is minted under both mets (as artcoo/b/15) and calm-ref-no, with different canonical ids
        var candidates = IdentifierCandidates.GetCandidates(id);

        candidates.Select(c => c.SourceSystem).Should()
            .Equal(SourceSystems.Mets, SourceSystems.CalmRefNo, SourceSystems.CalmAltRefNo);
        candidates.Should().OnlyContain(c => c.Value == expectedValue && c.Type == OntologyType.Work);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Input_Gives_No_Candidates(string id)
    {
        IdentifierCandidates.GetCandidates(id).Should().BeEmpty();
    }
}
