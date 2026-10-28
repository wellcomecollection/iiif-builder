using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using Utils;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories.Pdf.CoverPage;
using Wellcome.Dds.Server.Pdf;

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
            logger.LogInformation("No manifest for PDF cover page {Identifier}", ddsId.Value.LogSafe());
            return NotFound("Manifest for identifier not found");
        }

        var content = CoverPageContent.FromManifest(manifest, ddsId.Value);
        var logo = content.LogoUrl == null ? null : await logos.Get(content.LogoUrl);
        byte[] pdf;
        try
        {
            pdf = writer.Write(content, logo);
        }
        catch (Exception ex) when (logo != null)
        {
            // the logo was checked when fetched, so this should not happen; the page matters more than the logo
            logger.LogError(ex, "Cover page for {Identifier} failed with the logo; writing it without", ddsId.Value.LogSafe());
            pdf = writer.Write(content, null);
        }
        logger.LogInformation("Generated PDF cover page for {Identifier} ({Bytes} bytes)", ddsId.Value.LogSafe(), pdf.Length);
        return File(pdf, "application/pdf");
    }
}
