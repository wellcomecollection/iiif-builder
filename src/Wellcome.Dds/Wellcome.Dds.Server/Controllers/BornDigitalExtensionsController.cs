using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using Utils;
using Utils.Web;
using Wellcome.Dds.Common;
using Wellcome.Dds.Server.Images;

namespace Wellcome.Dds.Server.Controllers;

[FeatureGate(FeatureFlags.PresentationServices)]
[Route("extensions/born-digital")]
public class BornDigitalExtensionsController : ControllerBase
{
    private string WebRootPath { get; }
    private DdsOptions DdsOptions { get; }

    public BornDigitalExtensionsController(
        IWebHostEnvironment environment,
        IOptions<DdsOptions> options)
    {
        WebRootPath = environment.WebRootPath;
        DdsOptions = options.Value;
    }

    [HttpGet("placeholder-canvas/{*pathParts}")]
    public async Task<IActionResult> PlaceholderCanvas(string pathParts)
    {
        var parts = new PlaceholderParts(pathParts);
        var png = PlaceholderCanvasRenderer.RenderPng(GetFontPath(), parts.MimeType, parts.PromomKey);

        Response.ContentType = "image/png";
        if (DdsOptions.PlaceholderCanvasCacheTimeDays > 0)
        {
            Response.CacheForDays(DdsOptions.PlaceholderCanvasCacheTimeDays);
        }
        await Response.Body.WriteAsync(png);
        return new EmptyResult();
    }

    [HttpGet("placeholder-thumb/{*pathParts}")]
    public VirtualFileResult PlaceholderThumb(string pathParts)
    {
        var parts = new PlaceholderParts(pathParts);
        return parts.MimeMainType.ToLowerInvariant() switch
        {
            "image" => ServeThumb("image"),
            "video" => ServeThumb("video"),
            "audio" => ServeThumb("audio"),
            _ => ServeThumb("doc")
        };
    }

    private VirtualFileResult ServeThumb(string name)
    {
        if (DdsOptions.PlaceholderCanvasCacheTimeDays > 0)
        {
            Response.CacheForDays(DdsOptions.PlaceholderCanvasCacheTimeDays);
        }
        return File($"~/born-digital/{name}.png", "image/png");
    }

    private string GetFontPath()
    {
        // A TrueType conversion of the woff2 this used to ship, because libvips loads fonts through
        // FreeType, which does not read woff2.
        return System.IO.Path.Combine(WebRootPath, "born-digital", "wellcome-bold.ttf");
    }


    class PlaceholderParts
    {
        private int pronomNumber;
        // parts is like fmt/123/audio/mp3
        public string PromomPrefix { get; }
        public string PromomNumber { get; }
        public string PromomKey
        {
            get {
                if (pronomNumber <= 0)
                {
                    return "[Unknown format]";
                }

                return $"{PromomPrefix}/{PromomNumber}";}
        }

        public string MimeMainType { get; }
        public string MimeSubType { get; }
        public string MimeType => $"{MimeMainType}/{MimeSubType}";

        public PlaceholderParts(string pathParts)
        {
            var parts = pathParts.SplitByDelimiterIntoArray('/');
            if (parts.Length != 4)
            {
                throw new FormatException("Invalid placeholder format");
            }
            PromomPrefix = parts[0];
            PromomNumber = parts[1];
            MimeMainType = parts[2];
            MimeSubType = parts[3];
            if (PromomPrefix is not ("fmt" or "x-fmt"))
            {
                throw new FormatException("Invalid placeholder format");
            }

            if (!Int32.TryParse(PromomNumber, out pronomNumber))
            {
                throw new FormatException("Invalid placeholder format");
            }

            const int maxLength = 127; // according to RFC, but we might want to make this shorter
            if (MimeMainType.Length > maxLength || MimeSubType.Length > maxLength)
            {
                throw new FormatException("Invalid placeholder format");
            }
            if (MimeMainType.Contains(' ') || MimeSubType.Contains(' '))
            {
                throw new FormatException("Invalid placeholder format");
            }

        }
    }
}
