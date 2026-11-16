using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Wellcome.Dds.SmokeTests;

/// <summary>
/// Which deployed environment the smoke tests run against, and the items to use there.
/// SMOKE_ENV picks Fixtures/{name}.json (default "production"); SMOKE_HOST overrides its host.
/// </summary>
public static class SmokeEnvironment
{
    public static string Name { get; }
    public static SmokeFixtures Fixtures { get; }
    public static string Host => Fixtures.Host.TrimEnd('/');

    /// <summary>
    /// One client for every test. CloudFront refuses requests that have no User-Agent.
    /// </summary>
    public static HttpClient Http { get; }

    /// <summary>For checking a redirect itself, e.g. the dashboard's to the Entra login.</summary>
    public static HttpClient HttpNoRedirect { get; }

    static SmokeEnvironment()
    {
        Name = Environment.GetEnvironmentVariable("SMOKE_ENV") is { Length: > 0 } env ? env : "production";
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{Name}.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"No smoke test fixtures for environment '{Name}' ({path})");
        }

        Fixtures = JsonSerializer.Deserialize<SmokeFixtures>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (Environment.GetEnvironmentVariable("SMOKE_HOST") is { Length: > 0 } host)
        {
            Fixtures = Fixtures with { Host = host };
        }

        Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("wellcome-dds-smoke-test/1.0");
        HttpNoRedirect = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromSeconds(120) };
        HttpNoRedirect.DefaultRequestHeaders.UserAgent.ParseAdd("wellcome-dds-smoke-test/1.0");
    }

    public static string Url(string path) => $"{Host}/{path.TrimStart('/')}";
}

/// <summary>
/// The items each check uses. A check whose item isn't set for an environment is skipped there.
/// </summary>
public record SmokeFixtures
{
    public string Host { get; init; } = "";

    /// <summary>A Goobi monograph with ALTO text</summary>
    public string? GoobiManifest { get; init; }

    /// <summary>A Collection: a multi-volume work, or Chemist &amp; Druggist (title, volumes, issues)</summary>
    public string? Collection { get; init; }

    public string? BornDigitalManifest { get; init; }

    /// <summary>An AV item with a poster image</summary>
    public string? AvManifest { get; init; }

    /// <summary>An item and a word that search within should find</summary>
    public string? SearchItem { get; init; }
    public string? SearchTerm { get; init; }

    /// <summary>A born-digital PDF that /thumb renders</summary>
    public string? PdfThumbnailItem { get; init; }

    public string? CoverPageItem { get; init; }

    /// <summary>Open, with no size limit: deep zoom works</summary>
    public string? OpenImageItem { get; init; }

    /// <summary>Open, but limited to 1000px on purpose (player flags): maxWidth 1000, tiles refused</summary>
    public string? SizeLimitedItem { get; init; }

    /// <summary>Behind a login or clickthrough: auth services, tiles refused to anonymous users</summary>
    public string? AuthItem { get; init; }
}
