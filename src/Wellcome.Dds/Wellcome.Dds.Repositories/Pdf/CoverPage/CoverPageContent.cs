using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Utils;

namespace Wellcome.Dds.Repositories.Pdf.CoverPage;

/// <summary>
/// The text of a PDF cover page, extracted from a IIIF Presentation 3 Manifest the way the retired
/// PdfCoverPage service did it: first language of every language map, a fixed set of metadata labels,
/// requiredStatement with the "Attribution and usage" metadata as fallback, and the providers.
/// </summary>
public class CoverPageContent
{
    private const string NoTitle = "---NO TITLE---";
    private const string WellcomeCollection = "Wellcome Collection";

    public string Identifier { get; init; } = "";
    public string Title { get; init; } = NoTitle;

    /// <summary>
    /// Headed sections in display order: Contributors, Publication/Creation, Persistent URL, then one
    /// per additional provider. Values may contain HTML, which the writer strips.
    /// </summary>
    public List<CoverPageSection> Sections { get; } = new();

    /// <summary>
    /// The "License and attribution" lines, minus any that are just the provider's name.
    /// </summary>
    public List<string> Attribution { get; } = new();

    /// <summary>
    /// The first provider's label lines: name and postal address, printed in the footer.
    /// </summary>
    public List<string> ProviderAddress { get; } = new();

    public static CoverPageContent FromManifest(JObject manifest, string identifier)
    {
        var label = FirstLanguageValues(manifest["label"]);
        var providers = (manifest["provider"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
        var wellcome = providers.FirstOrDefault();

        var content = new CoverPageContent
        {
            Identifier = identifier,
            Title = label.Count > 0 ? string.Join(" - ", label) : NoTitle
        };

        var metadata = new Dictionary<string, List<string>>();
        foreach (var entry in (manifest["metadata"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            var key = FirstLanguageValues(entry["label"]).FirstOrDefault();
            if (key.HasText())
            {
                metadata[key] = FirstLanguageValues(entry["value"]);
            }
        }

        if (metadata.TryGetValue("Contributors", out var contributors) && contributors.Count > 0)
        {
            content.Sections.Add(new CoverPageSection("Contributors", contributors));
        }
        if (metadata.TryGetValue("Publication/creation", out var publication) && publication.Count > 0)
        {
            content.Sections.Add(new CoverPageSection("Publication/Creation", publication));
        }

        var homepage = (manifest["homepage"] as JArray)?.FirstOrDefault()?["id"]?.ToString();
        if (homepage.HasText())
        {
            content.Sections.Add(new CoverPageSection("Persistent URL", new List<string> { homepage }, homepage));
        }

        foreach (var additional in providers.Skip(1))
        {
            var lines = FirstLanguageValues(additional["label"]);
            if (lines.Count > 0)
            {
                content.Sections.Add(new CoverPageSection("Provider", lines));
            }
        }

        // prefer requiredStatement; fall back to the "Attribution and usage" metadata
        var statement = FirstLanguageValues(manifest["requiredStatement"]?["value"]);
        if (statement.Count == 0)
        {
            metadata.TryGetValue("Attribution and usage", out var attribution);
            statement = attribution ?? new List<string>();
        }
        content.Attribution.AddRange(statement.Where(s => s != WellcomeCollection));

        if (wellcome != null)
        {
            content.ProviderAddress.AddRange(FirstLanguageValues(wellcome["label"]));
        }

        return content;
    }

    /// <summary>
    /// The values of the first language in a IIIF language map ({ "en": [ ... ] }), or none.
    /// </summary>
    private static List<string> FirstLanguageValues(JToken? languageMap)
    {
        if (languageMap is not JObject map)
        {
            return new List<string>();
        }
        var first = map.Properties().FirstOrDefault()?.Value;
        return first switch
        {
            JArray array => array.Select(v => v.ToString()).Where(v => v.HasText()).ToList(),
            JValue value => new List<string> { value.ToString() },
            _ => new List<string>()
        };
    }
}

public class CoverPageSection
{
    public CoverPageSection(string heading, List<string> lines, string? linkUrl = null)
    {
        Heading = heading;
        Lines = lines;
        LinkUrl = linkUrl;
    }

    public string Heading { get; }
    public List<string> Lines { get; }

    /// <summary>
    /// When set, the section's text is a clickable link to this URL.
    /// </summary>
    public string? LinkUrl { get; }
}
