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
    }

    [Fact]
    public void Html_Fragments_Become_Plain_Paragraphs_Keeping_The_Links()
    {
        var paragraphs = CoverPageText.ToParagraphs(
            "<span>You may <a target=\"_top\" href=\"http://x.example/a?b=1&amp;c=2\">copy</a> this.<br/><br/>Second &amp;\n  last.<br/></span>");

        paragraphs.Select(p => p.Text).Should().Equal("You may copy this.", "Second & last.");
        paragraphs[0].Segments.Select(seg => (seg.Text, seg.Url)).Should().Equal(
            ("You may ", null),
            ("copy", "http://x.example/a?b=1&c=2"),
            (" this.", null));
        paragraphs[1].Segments.Should().ContainSingle().Which.Url.Should().BeNull();
    }

    [Fact]
    public void Characters_The_Font_Cannot_Show_Become_Question_Marks()
    {
        var font = CoverPageFonts.Regular;
        CoverPageText.Printable("Traité – “quotes” — Ελληνικά Кириллица", font).Should().Be("Traité – “quotes” — Ελληνικά Кириллица");
        // one '?' per character, including an emoji outside the BMP; whitespace becomes a plain space
        CoverPageText.Printable("日本語\t🙂", font).Should().Be("??? ?");
        // assigned code points Inter has no glyph for: ŉ (Afrikaans), a C1 control, an unassigned Greek slot
        CoverPageText.Printable("a\u0149b\u0080c\u0378", font).Should().Be("a?b?c?");
    }

    [Fact]
    public void Text_With_Characters_The_Font_Lacks_Still_Writes()
    {
        var manifest = JObject.Parse(BiocratsManifest);
        manifest["label"] = JObject.Parse("""{ "en": [ "Sch\u0149eider \u0080 日本" ] }""");

        var pdf = new CoverPageWriter().Write(CoverPageContent.FromManifest(manifest, "x"), null);

        using var document = PdfDocument.Open(pdf);
        document.GetPage(1).Text.Should().Contain("Sch?eider ? ??");
    }

    [Fact]
    public void More_Distinct_Characters_Than_A_Simple_Font_Can_Hold_Still_Writes()
    {
        // 303 distinct characters Inter has, across Latin, Latin-1, Greek and Cyrillic
        var alphabet = string.Concat(Enumerable.Range(0x21, 94).Select(c => (char) c))
            + string.Concat(Enumerable.Range(0xA1, 95).Select(c => (char) c))
            + string.Concat(Enumerable.Range(0x391, 25).Where(c => c != 0x3A2).Select(c => (char) c))
            + string.Concat(Enumerable.Range(0x3B1, 25).Select(c => (char) c))
            + string.Concat(Enumerable.Range(0x410, 64).Select(c => (char) c));
        alphabet.Distinct().Count().Should().BeGreaterThan(CoverPageWriter.MaxDistinctCharactersPerFont);
        var manifest = JObject.Parse(BiocratsManifest);
        manifest["metadata"]![2]!["value"] = new JObject { ["en"] = new JArray(alphabet) }; // not via JSON text: the alphabet has quotes and backslashes

        var pdf = new CoverPageWriter().Write(CoverPageContent.FromManifest(manifest, "x"), null);

        using var document = PdfDocument.Open(pdf);
        var text = document.GetPage(1).Text;
        text.Should().Contain("The biocrats", "the bold face is unaffected");
        text.Should().Contain("!\"#$%", "the first characters of the alphabet are set as they are");
        text.Should().Contain("??", "the characters past the limit become '?'");
    }

    [Fact]
    public void Writes_A_Pdf_With_Embedded_Inter_The_Text_And_The_Logo()
    {
        var content = CoverPageContent.FromManifest(JObject.Parse(BiocratsManifest), "b18035978");

        var pdf = new CoverPageWriter().Write(content);
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
        page.GetImages().Should().ContainSingle("the embedded Wellcome Collection logo");
        // the persistent URL, and the Creative Commons link inside the attribution text (one rectangle per line it spans)
        page.ExperimentalAccess.GetAnnotations().Where(a => a.Type == UglyToad.PdfPig.Annotations.AnnotationType.Link).Should().HaveCountGreaterOrEqualTo(2);
        var uris = System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/URI\s*\(([^)]*)\)").Select(m => m.Groups[1].Value);
        uris.Distinct().Should().BeEquivalentTo("https://wellcomecollection.org/works/zjytxny8", "http://creativecommons.org/licenses/by-nc/4.0/");
    }

    [Fact]
    public void A_Link_That_Wraps_Gets_A_Rectangle_On_Each_Line_Over_Its_Own_Words()
    {
        var manifest = JObject.Parse(BiocratsManifest);
        var linkText = string.Join(" ", Enumerable.Repeat("licence terms", 12));
        manifest["metadata"]![3]!["value"] = new JObject { ["en"] = new JArray($"Read the <a href=\"http://x.example/\">{linkText}</a> before use.") };

        var pdf = new CoverPageWriter().Write(CoverPageContent.FromManifest(manifest, "x"), null);

        using var document = PdfDocument.Open(pdf);
        var page = document.GetPage(1);
        var links = page.ExperimentalAccess.GetAnnotations().Where(a => a.Type == UglyToad.PdfPig.Annotations.AnnotationType.Link).ToList();
        links.Count.Should().BeGreaterThan(2, "the link text wraps over more than one line, plus the persistent URL");
        // every letter of the link text lies inside one of the link rectangles, and "Read the" and "before use." lie in none
        Func<UglyToad.PdfPig.Content.Letter, bool> covered = l => links.Any(a => a.Rectangle.Left <= l.GlyphRectangle.Left + 0.5 && a.Rectangle.Right >= l.GlyphRectangle.Right - 0.5
                                                                  && a.Rectangle.Bottom <= l.GlyphRectangle.Bottom + 0.5 && a.Rectangle.Top >= l.GlyphRectangle.Top - 0.5);
        var words = page.GetWords().ToList();
        words.Where(w => w.Text is "licence" or "terms").Should().HaveCount(24).And.OnlyContain(w => w.Letters.All(l => covered(l)));
        words.Where(w => w.Text is "Read" or "use.").Should().HaveCount(2).And.OnlyContain(w => !w.Letters.Any(l => covered(l)));
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
