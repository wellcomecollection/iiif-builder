using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wellcome.Dds.Repositories.Pdf.CoverPage;

namespace Wellcome.Dds.Server.Pdf;

/// <summary>
/// The provider logo a manifest refers to, fetched once per URL and kept for the process lifetime.
/// Registered as a singleton (a typed HttpClient would be transient, and so would its cache). A fetch
/// that fails, or returns something PdfPig cannot place (an SVG, an error page with status 200), is
/// remembered as "no logo" for a while so cover pages are produced without it rather than failing.
/// </summary>
public class CoverPageLogoCache
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The logo is served through CloudFront with a rule that rejects requests with no User-Agent
    /// (a 403), and HttpClient sends none by default.
    /// </summary>
    public const string UserAgent = "Wellcome-DDS/1.0 (iiif-builder PDF cover page; +https://github.com/wellcomecollection/iiif-builder)";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<CoverPageLogoCache> logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<Entry>>> cache = new();

    public CoverPageLogoCache(IHttpClientFactory httpClientFactory, ILogger<CoverPageLogoCache> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public async Task<byte[]?> Get(string url)
    {
        var entry = await cache.GetOrAdd(url, u => new Lazy<Task<Entry>>(() => Fetch(u))).Value;
        if (entry.Bytes == null && entry.RetryAt <= DateTime.UtcNow)
        {
            // the earlier attempt is stale: drop it and try once more
            cache.TryRemove(url, out _);
            entry = await cache.GetOrAdd(url, u => new Lazy<Task<Entry>>(() => Fetch(u))).Value;
        }
        return entry.Bytes;
    }

    private async Task<Entry> Fetch(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            using var response = await httpClientFactory.CreateClient(nameof(CoverPageLogoCache)).SendAsync(request);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (CoverPageWriter.IsUsableLogo(bytes))
            {
                logger.LogInformation("Fetched cover page logo {Url} ({Bytes} bytes)", url, bytes.Length);
                return new Entry(bytes, DateTime.MaxValue);
            }
            logger.LogWarning("Cover page logo {Url} is not a PNG or JPEG that can be placed ({Bytes} bytes); cover pages will have no logo", url, bytes.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch cover page logo {Url}; cover pages will have no logo", url);
        }
        return new Entry(null, DateTime.UtcNow + RetryAfter);
    }

    private record Entry(byte[]? Bytes, DateTime RetryAt);
}
