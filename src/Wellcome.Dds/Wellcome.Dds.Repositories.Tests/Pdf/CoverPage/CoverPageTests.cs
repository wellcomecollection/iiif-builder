using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using UglyToad.PdfPig;
using Wellcome.Dds.Repositories.Pdf.CoverPage;
using Xunit;

namespace Wellcome.Dds.Repositories.Tests.Pdf.CoverPage;

public class CoverPageTests
{
    // Trimmed from https://iiif.wellcomecollection.org/presentation/b18035978 (2026-10-28)
    private const string BiocratsManifest = """
        {
          "@context": "http://iiif.io/api/presentation/3/context.json",
          "id": "https://iiif.wellcomecollection.org/presentation/b18035978",
          "type": "Manifest",
          "label": { "en": [ "The biocrats / Gerald Leach." ] },
          "homepage": [ { "id": "https://wellcomecollection.org/works/zjytxny8", "type": "Text", "label": { "en": [ "The biocrats" ] } } ],
          "metadata": [
            { "label": { "en": [ "Publication/creation" ] }, "value": { "none": [ "London : Jonathan Cape, 1970." ] } },
            { "label": { "en": [ "Physical description" ] }, "value": { "en": [ "262 pages ; 23 cm" ] } },
            { "label": { "en": [ "Contributors" ] }, "value": { "none": [ "Leach, Gerald, 1933-" ] } },
            { "label": { "en": [ "Attribution and usage" ] }, "value": { "en": [
              "Wellcome Collection",
              "<span>You have permission to make copies of this work under a <a target=\"_top\" href=\"http://creativecommons.org/licenses/by-nc/4.0/\">Creative Commons, Attribution, Non-commercial license</a>.<br/><br/>Non-commercial use includes private study, academic research, teaching, and other activities that are not primarily intended for, or directed towards, commercial advantage or private monetary compensation.<br/><br/>Image source should be attributed as specified in the full catalogue record. If no source is given the image should be attributed to Wellcome Collection.</span>"
            ] } }
          ],
          "provider": [ {
            "id": "https://wellcomecollection.org",
            "type": "Agent",
            "label": { "en": [ "Wellcome Collection", "183 Euston Road", "London NW1 2BE UK", "T +44 (0)20 7611 8722", "E library@wellcomecollection.org", "https://wellcomecollection.org" ] },
            "logo": [ { "id": "https://iiif.wellcomecollection.org/logos/wellcome-collection-black.png", "type": "Image", "format": "image/png" } ]
          } ]
        }
        """;

    private static readonly string SamplePngPath = Path.Combine(AppContext.BaseDirectory, "Pdf", "CoverPage", "logo.png");

    [Fact]
    public void Content_Is_Extracted_Like_The_Python_Service_Did()
    {
        var content = CoverPageContent.FromManifest(JObject.Parse(BiocratsManifest), "b18035978");

        content.Title.Should().Be("The biocrats / Gerald Leach.");
        content.Sections.Select(s => s.Heading).Should().Equal("Contributors", "Publication/Creation", "Persistent URL");
        content.Sections[0].Lines.Should().Equal("Leach, Gerald, 1933-");
        content.Sections[1].Lines.Should().Equal("London : Jonathan Cape, 1970.");
        content.Sections[2].Lines.Should().Equal("https://wellcomecollection.org/works/zjytxny8");
        content.Sections[2].LinkUrl.Should().Be("https://wellcomecollection.org/works/zjytxny8");
        // "Wellcome Collection" is dropped from the attribution; the HTML statement is kept for the writer to flatten
        content.Attribution.Should().ContainSingle().Which.Should().StartWith("<span>You have permission");
        content.ProviderAddress.Should().HaveCount(6).And.StartWith("Wellcome Collection");
        content.LogoUrl.Should().Be("https://iiif.wellcomecollection.org/logos/wellcome-collection-black.png");
    }

    [Fact]
    public void Required_Statement_Takes_Precedence_And_Extra_Providers_Get_Sections()
    {
        var manifest = JObject.Parse(BiocratsManifest);
        manifest["requiredStatement"] = JObject.Parse("""{ "label": { "en": [ "Attribution" ] }, "value": { "en": [ "Wellcome Collection", "CC BY 4.0" ] } }""");
        ((JArray) manifest["provider"]!).Add(JObject.Parse("""{ "id": "https://example.org", "type": "Agent", "label": { "en": [ "Example Library", "1 Example Street" ] } }"""));

        var content = CoverPageContent.FromManifest(manifest, "b18035978");

        content.Attribution.Should().Equal("CC BY 4.0");
        content.Sections.Last().Heading.Should().Be("Provider");
        content.Sections.Last().Lines.Should().Equal("Example Library", "1 Example Street");
    }

    [Fact]
    public void A_Bare_Manifest_Still_Produces_Content()
    {
        var content = CoverPageContent.FromManifest(JObject.Parse("""{ "type": "Manifest" }"""), "x");

        content.Title.Should().Be("---NO TITLE---");
        content.Sections.Should().BeEmpty();
        content.Attribution.Should().BeEmpty();
        content.ProviderAddress.Should().BeEmpty();
        content.LogoUrl.Should().BeNull();
    }

    [Fact]
    public void Html_Fragments_Become_Plain_Paragraphs()
    {
        var paragraphs = CoverPageText.ToParagraphs(
            "<span>You may <a href=\"x\">copy</a> this.<br/><br/>Second &amp; last.<br/></span>");

        paragraphs.Should().Equal("You may copy this.", "Second & last.");
    }

    [Fact]
    public void Characters_The_Font_Cannot_Show_Become_Question_Marks()
    {
        CoverPageText.Printable("Traité – “quotes” — Ελληνικά Кириллица").Should().Be("Traité – “quotes” — Ελληνικά Кириллица");
        CoverPageText.Printable("日本語 🙂").Should().Be("??? ??");
    }

    [Fact]
    public void Writes_A_Pdf_With_Embedded_Inter_The_Text_And_The_Logo()
    {
        var content = CoverPageContent.FromManifest(JObject.Parse(BiocratsManifest), "b18035978");
        var logo = File.Exists(SamplePngPath) ? File.ReadAllBytes(SamplePngPath) : null;

        var pdf = new CoverPageWriter().Write(content, logo);
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "cover-page-sample.pdf"), pdf);

        pdf.Should().StartWith("%PDF-"u8.ToArray());
        using var document = PdfDocument.Open(pdf);
        document.NumberOfPages.Should().Be(1);
        document.Information.Title.Should().Be("b18035978.pdf");
        var page = document.GetPage(1);
        var text = page.Text;
        text.Should().Contain("The biocrats / Gerald Leach.");
        text.Should().Contain("Contributors").And.Contain("Leach, Gerald, 1933-");
        text.Should().Contain("Publication/Creation").And.Contain("London : Jonathan Cape, 1970.");
        text.Should().Contain("Persistent URL").And.Contain("https://wellcomecollection.org/works/zjytxny8");
        // page.Text joins wrapped lines without a space, so assert within a line
        text.Should().Contain("License and attribution").And.Contain("under a Creative Commons,").And.Contain("Attribution, Non-commercial license.");
        text.Should().NotContain("<span>").And.NotContain("<a ");
        text.Should().Contain("183 Euston Road");
        page.Letters.Select(l => l.FontName).Distinct().Should().BeEquivalentTo("Inter-Regular", "Inter-Bold");
        page.GetImages().Count().Should().Be(logo == null ? 0 : 1);
        page.ExperimentalAccess.GetAnnotations().Should().ContainSingle(a => a.Type == UglyToad.PdfPig.Annotations.AnnotationType.Link);
    }

    [Fact]
    public void Long_Text_Wraps_And_Overflows_Onto_A_Second_Page_With_The_Footer_Last()
    {
        var manifest = JObject.Parse(BiocratsManifest);
        var longValue = string.Join(" ", Enumerable.Repeat("Non-commercial use includes private study, academic research, teaching, and other activities.", 40));
        manifest["metadata"]!.Last!["value"] = JObject.Parse($$"""{ "en": [ "{{longValue}}" ] }""");

        var pdf = new CoverPageWriter().Write(CoverPageContent.FromManifest(manifest, "b18035978"), null);

        using var document = PdfDocument.Open(pdf);
        document.NumberOfPages.Should().BeGreaterThan(1);
        var last = document.GetPage(document.NumberOfPages);
        last.Text.Should().Contain("183 Euston Road", "the footer is on the last page");
        document.GetPage(1).Text.Should().NotContain("183 Euston Road");
        // nothing ran off the right edge
        foreach (var letter in document.GetPages().SelectMany(p => p.Letters))
        {
            letter.GlyphRectangle.Right.Should().BeLessThanOrEqualTo(595.28 - 70);
        }
    }
}
