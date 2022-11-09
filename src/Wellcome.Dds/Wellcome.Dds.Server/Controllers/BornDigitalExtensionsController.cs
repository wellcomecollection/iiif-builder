using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Utils;

namespace Wellcome.Dds.Server.Controllers;

[FeatureGate(FeatureFlags.PresentationServices)]
[Route("extensions/born-digital")]
public class BornDigitalExtensionsController : ControllerBase
{
    private string WebRootPath { get; set; }
    
    public BornDigitalExtensionsController(IWebHostEnvironment environment)
    {
        WebRootPath = environment.WebRootPath;
    }
    
    [HttpGet("placeholder-canvas/{*pathParts}")]
    public async Task<IActionResult> PlaceholderCanvas(string pathParts)
    {
        var parts = new PlaceholderParts(pathParts);
        using Image img = new Image<Rgba32>(1000, 800);
        FontCollection collection = new();
        FontFamily family = collection.Add(GetFontPath());
        Font largeFont = family.CreateFont(32, FontStyle.Regular);
        Font smallFont = family.CreateFont(16, FontStyle.Regular);
        img.Mutate(x=> x.DrawText(
            $"File type: {parts.MimeType}", 
            largeFont, Color.Black, new PointF(20, 20)));
        img.Mutate(x=> x.DrawText(
            $"PRONOM key: {parts.PromomKey}", 
            largeFont, Color.Black, new PointF(20, 80)));
        img.Mutate(x=> x.DrawText(
            $"This is a placeholder canvas.", 
            smallFont, Color.Black, new PointF(20, 140)));
        img.Mutate(x=> x.DrawText(
            $"The original file is available in the rendering property of this canvas.", 
            smallFont, Color.Black, new PointF(20, 165)));
        
        Response.ContentType = "image/png";
        await img.SaveAsync(Response.Body, new PngEncoder());
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
        return File($"~/born-digital/{name}.png", "image/png");
    }

    private string GetFontPath()
    {
        return System.IO.Path.Combine(WebRootPath, "born-digital", "wellcome-bold.woff2");
    }
    

    class PlaceholderParts
    {
        // parts is like fmt/123/audio/mp3
        public string PromomPrefix { get; set; }
        public string PromomNumber { get; set; }
        public string PromomKey => $"{PromomPrefix}/{PromomNumber}";
        
        public string MimeMainType { get; set; }
        public string MimeSubType { get; set; }
        public string MimeType => $"{MimeMainType}/{MimeSubType}";

        public PlaceholderParts(string pathParts)
        {
            var parts = pathParts.SplitByDelimiterIntoArray('/');
            PromomPrefix = parts[0];
            PromomNumber = parts[1];
            MimeMainType = parts[2];
            MimeSubType = parts[3];
        }
    }
}