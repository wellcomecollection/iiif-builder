using System.Linq;
using FluentAssertions;
using Wellcome.Dds.Catalogue;
using Xunit;

namespace Wellcome.Dds.Tests.Catalogue;

public class WorkSelectionTests
{
    private static Work Work(string id, string? iiifLocation = null, params (string type, string value)[] identifiers)
    {
        var work = new Work
        {
            Id = id,
            Identifiers = identifiers.Select(i => new Identifier { IdentifierType = new LabelledEntity { Id = i.type }, Value = i.value }).ToArray()
        };
        if (iiifLocation != null)
        {
            work.Items = new[]
            {
                new Item { Locations = new[] { new Location { LocationType = new LabelledEntity { Id = "iiif-presentation" }, Url = iiifLocation } } }
            };
        }
        return work;
    }

    [Fact]
    public void No_Results_Chooses_Nothing_And_Is_Not_Ambiguous()
    {
        var selection = WorkSelection.Choose("b12345678", null);

        selection.Chosen.Should().BeNull();
        selection.Ambiguous.Should().BeFalse();
        selection.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void A_Single_Result_Is_Chosen_Whatever_Its_Strength()
    {
        var selection = WorkSelection.Choose("b12345678", new[] { Work("aaaaaaaa") });

        selection.Chosen!.Id.Should().Be("aaaaaaaa");
        selection.Strength.Should().Be(WorkMatchStrength.None);
        selection.Others.Should().BeEmpty();
    }

    [Fact]
    public void The_Work_Whose_Iiif_Location_Names_The_Identifier_Beats_One_That_Merely_Carries_It()
    {
        var digitised = Work("dddddddd", "https://iiif.wellcomecollection.org/presentation/b16784613");
        var related = Work("rrrrrrrr", null, ("sierra-system-number", "b16784613"));

        var selection = WorkSelection.Choose("b16784613", new[] { related, digitised });

        selection.Chosen!.Id.Should().Be("dddddddd");
        selection.Strength.Should().Be(WorkMatchStrength.PresentationLocation);
        selection.Others.Select(w => w.Id).Should().Equal("rrrrrrrr");
        selection.Reason.Should().Contain("IIIF location");
    }

    [Theory]
    [InlineData("https://wellcomelibrary.org/iiif/b16784613/manifest")]
    [InlineData("https://iiif.wellcomecollection.org/presentation/v2/b16784613")]
    [InlineData("https://iiif.wellcomecollection.org/presentation/B16784613")]
    public void Every_Form_Of_Iiif_Location_Counts(string url)
    {
        WorkSelection.GetStrength(Work("x", url), "b16784613").Should().Be(WorkMatchStrength.PresentationLocation);
    }

    [Fact]
    public void A_Calm_Ref_In_A_Location_Is_Matched_After_Unescaping()
    {
        var work = Work("x", "https://iiif.wellcomecollection.org/presentation/ARTCOO%2FB%2F15");

        WorkSelection.GetStrength(work, "ARTCOO/B/15").Should().Be(WorkMatchStrength.PresentationLocation);
    }

    [Fact]
    public void Identifier_Types_Rank_Mets_Then_Sierra_Then_CalmRef_Then_AltRef_Then_Anything()
    {
        var id = "SA/MAC/J/3/32";
        var byMets = Work("m", null, ("mets", "sa/mac/j/3/32"));
        var bySierra = Work("s", null, ("sierra-system-number", id));
        var byRef = Work("r", null, ("calm-ref-no", id));
        var byAlt = Work("a", null, ("calm-altref-no", id));
        var byOther = Work("o", null, ("wellcome-digcode", id));

        WorkSelection.GetStrength(byMets, id).Should().Be(WorkMatchStrength.Mets);
        WorkSelection.GetStrength(bySierra, id).Should().Be(WorkMatchStrength.SierraSystemNumber);
        WorkSelection.GetStrength(byRef, id).Should().Be(WorkMatchStrength.CalmRefNo);
        WorkSelection.GetStrength(byAlt, id).Should().Be(WorkMatchStrength.CalmAltRefNo);
        WorkSelection.GetStrength(byOther, id).Should().Be(WorkMatchStrength.OtherIdentifier);

        var selection = WorkSelection.Choose(id, new[] { byOther, byAlt, byRef, bySierra, byMets });

        selection.Chosen!.Id.Should().Be("m");
        selection.Candidates.Select(w => w.Id).Should().Equal("m", "s", "r", "a", "o");
    }

    [Fact]
    public void The_Short_Form_Sierra_Identifier_Counts_As_The_System_Number()
    {
        // b14658197's sierra-identifier is 1465819: the rule the old implementation was written for
        var work = Work("x", null, ("sierra-identifier", "1465819"));
        var other = Work("y", null, ("sierra-identifier", "9999999"));

        WorkSelection.GetStrength(work, "b14658197").Should().Be(WorkMatchStrength.SierraSystemNumber);
        WorkSelection.GetStrength(other, "b14658197").Should().Be(WorkMatchStrength.None);
        WorkSelection.Choose("b14658197", new[] { other, work }).Chosen!.Id.Should().Be("x");
    }

    [Fact]
    public void Two_Works_Bound_Equally_Strongly_Is_Ambiguous_Not_A_Guess()
    {
        var one = Work("11111111", null, ("sierra-system-number", "b12345678"));
        var two = Work("22222222", null, ("sierra-system-number", "b12345678"));

        var selection = WorkSelection.Choose("b12345678", new[] { one, two });

        selection.Chosen.Should().BeNull();
        selection.Ambiguous.Should().BeTrue();
        selection.Reason.Should().Contain("11111111").And.Contain("22222222");
    }

    [Fact]
    public void Several_Works_None_Of_Which_Shows_How_It_Matched_Is_Ambiguous()
    {
        var selection = WorkSelection.Choose("b12345678", new[] { Work("11111111"), Work("22222222") });

        selection.Chosen.Should().BeNull();
        selection.Ambiguous.Should().BeTrue();
    }

    [Fact]
    public void The_Work_Type_K_Guess_Is_Gone()
    {
        var k = new Work { Id = "kkkkkkkk", WorkType = new LabelledEntity { Id = "k" } };
        var carrier = Work("cccccccc", null, ("calm-altref-no", "SA/MAC/J.3/32"));

        var selection = WorkSelection.Choose("SA/MAC/J.3/32", new[] { k, carrier });

        selection.Chosen!.Id.Should().Be("cccccccc");
    }
}
