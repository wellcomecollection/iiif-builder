using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using Newtonsoft.Json.Linq;
using Utils;
using Utils.Caching;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Controllers;


[FeatureGate(FeatureFlags.PresentationServices)]
[Route("[controller]")]
[ApiController]
public class SvgController : ControllerBase
{
    private readonly DdsOptions ddsOptions;
    private Helpers helpers;
    private readonly ISimpleCache cache;
    private ILogger<SvgController> logger;

    private static XmlWriterSettings _xmlWriterSettings = new XmlWriterSettings()
    {
        Indent = true,
        OmitXmlDeclaration = true
    };
    
    public SvgController(
        IOptions<DdsOptions> options,
        Helpers helpers,
        ISimpleCache cache,
        ILogger<SvgController> logger)
    {
        ddsOptions = options.Value;
        this.helpers = helpers;
        this.cache = cache;
        this.logger = logger;
    }
    
    [HttpGet("{identifier}/{assetIdentifier}")]
    public async Task<IActionResult> GetTextAsSvg(string identifier, string assetIdentifier)
    {
        // load the v3 Key
        var key = $"v3/{identifier}/{assetIdentifier}/line";
        var v3Annotations = await helpers.LoadAsJson(ddsOptions.AnnotationContainer, key);

        if (v3Annotations == null)
        {
            return NotFound($"No annotation page available for {identifier}/{assetIdentifier}.");
        }
        
        
        // Get the width and height from v3Annos new extra properties
        // if not present, get from map
        var size = await GetCanvasDimensions(v3Annotations, identifier, assetIdentifier);
        if (size is { Width: not null, Height: not null, Width: > 0, Height: > 0 })
        {
            var xmlString = GetXmlString(size.Width.Value, size.Height.Value, v3Annotations);
            return Content(xmlString, "image/svg+xml");
        }
        return NotFound($"No XML SVG for {identifier}/{assetIdentifier}.");
        
        
    }

       private string GetXmlString(int width, int height, JObject v3Annos)
    {
        var sb = new StringBuilder();

        const string invisibleStyle = @"                                     
.text-line-segment { fill: rgba(0,0,0,0) }
.text-line-segment::selection { fill: #fff; background: rgba(15, 76, 155, 0.8) }
  ";
        
        using (XmlWriter writer = XmlWriter.Create(sb, _xmlWriterSettings))
        {
            writer.WriteStartElement("svg", "http://www.w3.org/2000/svg");
            // writer.WriteAttributeString("width", $"{wh.Item1}px");
            // writer.WriteAttributeString("height", $"{wh.Item2}px");
            writer.WriteAttributeString("viewBox", $"0 0 {width} {height}");
            var items = v3Annos["items"];
            if (items != null)
            { 
                foreach (var jAnno in items)
                {
                    var anno = (JObject) jAnno;
                    var target = anno["target"]?.Value<string>()?.Split("=")[^1];
                    if (target == null)
                    {
                        continue;
                    }
                    var xywh = target.Split(",");
                    if (xywh.Length != 4)
                    {
                        continue;
                    }
                    var body = anno["body"]?["value"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        continue;
                    }
                    writer.WriteStartElement("text");
                    writer.WriteAttributeString("x", xywh[0]);
                    var y = Convert.ToInt32(xywh[1]);
                    var h = Convert.ToInt32(xywh[3]);
                    writer.WriteAttributeString("y", $"{(int)(y + h * 0.75)}");
                    writer.WriteAttributeString("textLength", xywh[2]);
                    writer.WriteAttributeString("font-size", xywh[3]);
                    writer.WriteAttributeString("lengthAdjust", "spacingAndGlyphs");
                    writer.WriteAttributeString("class", "text-line-segment");
                    writer.WriteString(body);
                    writer.WriteEndElement();
                }
                writer.WriteStartElement("style");
                writer.WriteString(invisibleStyle);
                writer.WriteEndElement();
                
            }   
            writer.WriteEndElement();
        }

        return sb.ToString();
    }
    
        
    private async Task<(int? Width, int? Height)> GetCanvasDimensions(
        JObject annotationPage, string manifestIdentifier, string assetIdentifier)
    {
        int? width = null;
        int? height = null;
        
        var linkedCanvas = annotationPage["partOf"]?[0];
        if (linkedCanvas != null && linkedCanvas["type"]?.Value<string>() == "Canvas")
        {
            width = linkedCanvas["width"]?.Value<int>();
            height = linkedCanvas["height"]?.Value<int>();
        }

        if (height != null && width is > 0 && height > 0)
        {
            return (width.Value, height.Value);
        }
        
        // The Annotation page doesn't reference a canvas for width and height measurements,
        // so we need to obtain that information from the Manifest itself. We'll only do this
        // once per manifest, and cache a map of all the sizes.
        Dictionary<string, Tuple<int, int>> sizeMap =
            await cache.GetCached(20, $"sizemap-{manifestIdentifier}",
                () => GetSizeMap(manifestIdentifier));
        
        if(sizeMap.TryGetValue(assetIdentifier, out var wh))
        {
            return (wh.Item1, wh.Item2);
        }
        return (0, 0);
    }
    
    
    private async Task<Dictionary<string, Tuple<int, int>>> GetSizeMap(string identifier)
    {
        var start = DateTime.Now;
        var manifest = await helpers.LoadAsJson(ddsOptions.PresentationContainer, $"v3/{identifier}");
        var dict = new Dictionary<string, Tuple<int, int>>();
        foreach (var jCanvas in manifest["items"])
        {
            var canvas = (JObject)jCanvas;
            var assetId = canvas["id"].Value<string>().Split('/')[^1];
            var w = canvas["width"].Value<int>();
            var h = canvas["height"].Value<int>();
            dict.Add(assetId, new(w, h));
        }

        var timeTaken = (DateTime.Now - start).Milliseconds;
        logger.LogDebug("Sizemap built in {timeTaken} ms with {dictSize} entries", timeTaken, dict.Keys.Count);

        return dict;
    }
}