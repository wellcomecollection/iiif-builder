using FluentAssertions;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories.Mets.Model;
using Xunit;

namespace Wellcome.Dds.AssetDomainRepositories.Tests.Mets
{
    public class PeriodicalIssueLicenceTests
    {
        // LogicalStructDiv gives periodical sections with no licence "CC-BY-NC" and marks it as a default
        private static ISectionMetadata FromMets(string licence) => new BornDigitalSectionMetadata { DzLicenseCode = licence };
        private static ISectionMetadata Defaulted() =>
            new BornDigitalSectionMetadata { DzLicenseCode = "CC-BY-NC", DzLicenseCodeIsDefault = true };

        [Fact]
        public void Issue_Licence_From_Mets_Wins()
        {
            FromMets("PDM").GetPeriodicalIssueLicence(FromMets("In copyright")).Should().Be("PDM");
        }

        [Fact]
        public void Restrictive_Issue_Licence_From_Mets_Wins()
        {
            FromMets("In copyright").GetPeriodicalIssueLicence(FromMets("PDM")).Should().Be("In copyright");
        }

        [Fact]
        public void Volume_Licence_Used_When_Issue_Only_Has_Default()
        {
            // The case the old code got wrong: an "In copyright" volume made its issues CC-BY-NC
            Defaulted().GetPeriodicalIssueLicence(FromMets("In copyright")).Should().Be("In copyright");
        }

        [Fact]
        public void Volume_Licence_Used_When_Issue_Has_None()
        {
            FromMets(null!).GetPeriodicalIssueLicence(FromMets("PDM")).Should().Be("PDM");
        }

        [Fact]
        public void Default_Used_When_Volume_Only_Has_Default()
        {
            Defaulted().GetPeriodicalIssueLicence(Defaulted()).Should().Be("CC-BY-NC");
        }

        [Fact]
        public void Default_Used_When_No_Volume()
        {
            Defaulted().GetPeriodicalIssueLicence(null).Should().Be("CC-BY-NC");
        }
    }
}
