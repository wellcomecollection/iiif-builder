using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Pdf.CoverPage;

namespace Wellcome.Dds.Server.Controllers;

/// <summary>
/// The cover page the DLCS prepends to a generated PDF of a digitised work. Built from the work's
/// IIIF Manifest in the presentation bucket. Replaces the PdfCoverPage Python service, at the same
/// path, so pointing the DLCS named query at this host is the only change needed.
/// </summary>
[FeatureGate(FeatureFlags.PresentationServices)]
[Route("pdf-cover")]
public class PdfCoverController : ControllerBase
{
    private readonly Helpers helpers;
    private readonly DdsOptions ddsOptions;
    private readonly IIdentityService identityService;
    private readonly CoverPageWriter writer;
    private readonly CoverPageLogoCache logos;
    private readonly ILogger<PdfCoverController> logger;

    public PdfCoverController(
        Helpers helpers,
        IOptions<DdsOptions> ddsOptions,
        IIdentityService identityService,
        CoverPageWriter writer,
        CoverPageLogoCache logos,
        ILogger<PdfCoverController> logger)
    {
        this.helpers = helpers;
        this.ddsOptions = ddsOptions.Value;
        this.identityService = identityService;
        this.writer = writer;
        this.logos = logos;
        this.logger = logger;
    }

    /// <summary>
    /// The retired service's health check path; kept so nothing that still probes it sees a 404.
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "working" });

    [HttpGet("{*id}")]
    public async Task<IActionResult> Get(string id)
    {
        DdsIdentity ddsId;
        try
        {
            ddsId = identityService.GetIdentity(id);
        }
        catch (FormatException)
        {
            return NotFound($"Not a valid identifier: {id}");
        }

        var manifest = await helpers.LoadAsJson(ddsOptions.PresentationContainer, $"v3/{ddsId}");
        if (manifest == null)
        {
            logger.LogInformation("No manifest for PDF cover page {Identifier}", ddsId.Value);
            return NotFound("Manifest for identifier not found");
        }

        var content = CoverPageContent.FromManifest(manifest, ddsId.Value);
        var logo = content.LogoUrl == null ? null : await logos.Get(content.LogoUrl);
        var pdf = writer.Write(content, logo);
        logger.LogInformation("Generated PDF cover page for {Identifier} ({Bytes} bytes)", ddsId.Value, pdf.Length);
        return File(pdf, "application/pdf");
    }
}

/// <summary>
/// The provider logo referenced by a manifest, fetched once per URL and kept for the process lifetime.
/// A failed fetch is logged and the cover page is produced without the logo.
/// </summary>
public class CoverPageLogoCache
{
    private readonly HttpClient httpClient;
    private readonly ILogger<CoverPageLogoCache> logger;
    private readonly ConcurrentDictionary<string, Task<byte[]?>> cache = new();

    public CoverPageLogoCache(HttpClient httpClient, ILogger<CoverPageLogoCache> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public Task<byte[]?> Get(string url)
    {
        return cache.GetOrAdd(url, Fetch);
    }

    private async Task<byte[]?> Fetch(string url)
    {
        try
        {
            var bytes = await httpClient.GetByteArrayAsync(url);
            logger.LogInformation("Fetched cover page logo {Url} ({Bytes} bytes)", url, bytes.Length);
            return bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch cover page logo {Url}; cover pages will have no logo until restart", url);
            cache.TryRemove(url, out _); // so the next request tries again
            return null;
        }
    }
}
