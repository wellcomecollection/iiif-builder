using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.Common.Tests
{
    public class LicensesAndOptionsTests
    {
        private const string CurrentView = "currentViewAsJpg";
        private const string HighRes = "wholeImageHighResAsJpg";
        private const string LowRes = "wholeImageLowResAsJpg";
        private const string Pdf = "entireDocumentAsPdf";
        private const string Original = "entireFileAsOriginal";

        private readonly LicensesAndOptions sut = LicensesAndOptions.Instance;

        // Letter codes: behaviour unchanged

        [Theory]
        [InlineData("a")]
        [InlineData("A")]
        [InlineData("k")]
        [InlineData("s")]
        public void LetterCode_Monograph_PermitsEverythingIncludingPdf(string code)
        {
            sut.GetPermittedOperations(code, "Monograph", "image/jp2")
                .Should().BeEquivalentTo(CurrentView, HighRes, LowRes, Pdf);
        }

        [Fact]
        public void LetterCode_Monograph_e_PermitsImagesButNotPdf()
        {
            sut.GetPermittedOperations("e", "Monograph", "image/jp2")
                .Should().BeEquivalentTo(CurrentView, HighRes, LowRes);
        }

        [Theory]
        [InlineData("l")]
        [InlineData("q")]
        public void LetterCode_Artwork_l_to_q_DoesNotPermitHighRes(string code)
        {
            sut.GetPermittedOperations(code, "Artwork", "image/jp2")
                .Should().BeEquivalentTo(CurrentView, LowRes);
        }

        [Theory]
        [InlineData("r")]
        [InlineData("s")]
        public void LetterCode_PdfMonograph_PermitsOriginalOnly(string code)
        {
            sut.GetPermittedOperations(code, "Monograph", "application/pdf")
                .Should().BeEquivalentTo(Original);
        }

        [Fact]
        public void LetterCode_UnknownForSectionType_PermitsNothing()
        {
            sut.GetPermittedOperations("z", "Monograph", "image/jp2").Should().BeEmpty();
        }

        [Fact]
        public void LetterCode_SectionTypeNotInTable_PermitsNothing()
        {
            sut.GetPermittedOperations("a", "PeriodicalIssue", "image/jp2").Should().BeEmpty();
        }

        [Theory]
        [InlineData("BoundManuscript")]
        [InlineData("boundManuscript")]
        [InlineData("boundmanuscript")]
        public void LetterCode_BoundManuscript_IsFoundWhateverTheCase(string sectionType)
        {
            // The table key was "boundManuscript" but lookups were lower-cased, so it never matched
            sut.GetPermittedOperations("a", sectionType, "image/jp2")
                .Should().BeEquivalentTo(CurrentView, HighRes, LowRes);
        }

        // Licence names: the open ones now permit what an open letter code permits

        [Theory]
        [InlineData("PDM")]
        [InlineData("pdm")]
        [InlineData("CC0")]
        [InlineData("CC-0")]
        [InlineData("CC-BY")]
        [InlineData("CC-BY-NC")]
        [InlineData("CC-BY-NC-ND")]
        [InlineData("OGL")]
        [InlineData("OPL")]
        [InlineData(" PDM ")]
        public void OpenLicenceName_Monograph_PermitsEverythingIncludingPdf(string licence)
        {
            sut.GetPermittedOperations(licence, "Monograph", "image/jp2")
                .Should().BeEquivalentTo(CurrentView, HighRes, LowRes, Pdf);
        }

        [Theory]
        [InlineData("CC-BY-ND", "e")]
        [InlineData("CC-BY-SA", "f")]
        [InlineData("CC-BY-NC-SA", "g")]
        public void OpenLicenceName_Monograph_NoPdf_WhereTheLetterCodeHadNone(string licence, string letter)
        {
            var expected = new[] { CurrentView, HighRes, LowRes };
            sut.GetPermittedOperations(licence, "Monograph", "image/jp2").Should().BeEquivalentTo(expected);
            sut.GetPermittedOperations(letter, "Monograph", "image/jp2").Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void OpenLicenceName_Periodical_NoPdf_WhereTheLetterCodeHadNone()
        {
            sut.GetPermittedOperations("CC-BY-SA", "PeriodicalIssue", "image/jp2")
                .Should().BeEquivalentTo(CurrentView, HighRes, LowRes);
        }

        [Fact]
        public void OpenLicenceName_Artwork_PermitsHighRes_Deliberately()
        {
            // A licence name can't say what the artwork letters l-q said (no high-res); per decision D1 every open
            // licence name permits high-res. Artworks restricted on purpose do it with player flags.
            sut.GetPermittedOperations("CC-BY-NC", "Artwork", "image/jp2").Should().Contain(HighRes);
        }

        [Fact]
        public void OpenLicenceName_Transcript_PermitsOriginalOnly()
        {
            // Transcripts aren't image sequences, so no "View as PDF" rendering
            sut.GetPermittedOperations("CC-BY", "Transcript", "application/pdf")
                .Should().BeEquivalentTo(Original);
        }

        [Theory]
        [InlineData("PeriodicalIssue")]
        [InlineData("Archive")]
        [InlineData("MultipleVolumeMultipleCopy")]
        public void OpenLicenceName_OtherImageSections_PermitsEverythingIncludingPdf(string sectionType)
        {
            // Not dependent on the letter-code table, which has no entry for most of these
            sut.GetPermittedOperations("CC-BY-NC", sectionType, "image/jp2")
                .Should().BeEquivalentTo(CurrentView, HighRes, LowRes, Pdf);
        }

        [Theory]
        [InlineData("Artwork")]
        [InlineData("BoundManuscript")]
        public void OpenLicenceName_ArtworkAndBoundManuscript_PermitsImagesButNotPdf(string sectionType)
        {
            sut.GetPermittedOperations("PDM", sectionType, "image/jp2")
                .Should().BeEquivalentTo(CurrentView, HighRes, LowRes);
        }

        [Theory]
        [InlineData("Video", "video/mp4")]
        [InlineData("Audio", "audio/mpeg")]
        [InlineData("Monograph", "application/pdf")]
        public void OpenLicenceName_AvAndPdfMonograph_PermitsOriginalOnly(string sectionType, string assetType)
        {
            sut.GetPermittedOperations("CC-BY", sectionType, assetType)
                .Should().BeEquivalentTo(Original);
        }

        [Fact]
        public void PeriodicalDefaultLicence_PermitsHighRes()
        {
            // LogicalStructDiv gives periodicals with no dz code the licence "CC-BY-NC"; Chemist & Druggist depends on this
            sut.GetPermittedOperations("CC-BY-NC", "PeriodicalIssue", "image/jp2")
                .Should().Contain(HighRes);
        }

        [Theory]
        [InlineData("ARR")]
        [InlineData("All Rights Reserved")]
        [InlineData("In copyright")]
        [InlineData("SOMETHING-ELSE")]
        public void OtherLicenceNames_PermitNothing(string licence)
        {
            sut.GetPermittedOperations(licence, "Monograph", "image/jp2").Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void EmptyCode_PermitsNothing(string code)
        {
            sut.GetPermittedOperations(code, "Monograph", "image/jp2").Should().BeEmpty();
        }

        // Player flags still take precedence in Manifestation; check the flags path is untouched

        [Fact]
        public void PlayerFlags_EmbedOnly_PermitsEmbedOnly()
        {
            sut.GetPermittedOperations((int)PlayerOptions.Embed, "image/jp2")
                .Should().BeEquivalentTo("embed");
        }
    }
}
