using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Wellcome.Dds.Repositories.Pdf.CoverPage;

namespace Wellcome.Dds.Server.Pdf;

/// <summary>
/// The provider logo a manifest refers to, read once per URL and kept for the process lifetime.
/// The Wellcome Collection logo the DDS puts in every manifest is a file this server serves itself
/// (wwwroot/logos), so it is read from the web root rather than fetched through the edge and back;
/// a logo hosted elsewhere is fetched over HTTP. Registered as a singleton (a typed HttpClient would
/// be transient, and so would its cache). A fetch that fails, or bytes PdfPig cannot place (an SVG,
/// an error page with status 200), are remembered as "no logo" for a while so cover pages are
/// produced without it rather than failing.
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
    private readonly IWebHostEnvironment environment;
    private readonly ILogger<CoverPageLogoCache> logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<Entry>>> cache = new();

    public CoverPageLogoCache(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment, ILogger<CoverPageLogoCache> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.environment = environment;
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
            var (bytes, from) = await ReadFromWebRoot(url) ?? await FetchOverHttp(url);
            if (CoverPageWriter.IsUsableLogo(bytes))
            {
                logger.LogInformation("Cover page logo {Url} read from {From} ({Bytes} bytes)", url, from, bytes.Length);
                return new Entry(bytes, DateTime.MaxValue);
            }
            logger.LogWarning("Cover page logo {Url} ({From}) is not a PNG or JPEG that can be placed ({Bytes} bytes); cover pages will have no logo", url, from, bytes.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch cover page logo {Url}; cover pages will have no logo", url);
        }
        return new Entry(null, DateTime.UtcNow + RetryAfter);
    }

    /// <summary>
    /// The file this server would serve for the URL's path, if there is one under the web root.
    /// </summary>
    private async Task<(byte[] Bytes, string From)?> ReadFromWebRoot(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }
        var file = environment.WebRootFileProvider?.GetFileInfo(Uri.UnescapeDataString(uri.AbsolutePath));
        if (file is not { Exists: true, IsDirectory: false })
        {
            return null;
        }
        await using var stream = file.CreateReadStream();
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return (buffer.ToArray(), $"web root ({file.PhysicalPath ?? file.Name})");
    }

    private async Task<(byte[] Bytes, string From)> FetchOverHttp(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        using var response = await httpClientFactory.CreateClient(nameof(CoverPageLogoCache)).SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsByteArrayAsync(), "HTTP");
    }

    private record Entry(byte[]? Bytes, DateTime RetryAt);
}
