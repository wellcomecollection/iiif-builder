using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Utils;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.Dashboard.Models;

namespace Wellcome.Dds.Dashboard.Controllers;

[Route("[controller]")]
public class AssetController : Controller
{
    private readonly ILogger<AssetController> logger;
    private readonly IDlcs dlcs;
    private readonly DlcsOptions dlcsOptions;
    private readonly HttpClient httpClient;
    
    public AssetController(
        ILogger<AssetController> logger,
        IDlcs dlcs,
        IOptions<DlcsOptions> options,
        HttpClient httpClient)
    {
        this.logger = logger;
        this.dlcs = dlcs;
        dlcsOptions = options.Value;
        this.httpClient = httpClient;
    }

    [HttpGet]
    [Route("{space}/{id}")]
    public async Task<ActionResult> Index(int space, string id)
    {
        var imgCallContext = new DlcsCallContext("AssetController::Index-GetImage", $"{space}/{id}");
        var image = await dlcs.GetImage(space, id, imgCallContext);
        // var storageCallContext = new DlcsCallContext("AssetController::Index-GetImageStorage", $"{space}/{id}");
        // var storage = await dlcs.GetImageStorage(space, id, storageCallContext);
        var model = new AssetModel
        {
            Space = space,
            ModelId = id,
            Asset = image,
            DlcsPortalPage = string.Format(dlcsOptions.PortalPageTemplate!, space, image?.StorageIdentifier),
            SingleAssetManifest = string.Format(dlcsOptions.SingleAssetManifestTemplate!, space, image?.StorageIdentifier),
            Thumbnails = await GetThumbnails(image?.ThumbnailImageService)
            // Storage = some new storage object from API,
            // see https://api.dlcs.io/customers/2/spaces/5/images/b31404777_0001.jp2/storage and 
            // https://api.dlcs.io/customers/2/spaces/5/images/b20018484_0055-0000-7064-0000-0-0000-0000-0.mpg/storage
            // (latter does not work)
        };
        return View(model);
    }

    private async Task<List<Thumbnail>> GetThumbnails(string thumbnailInfoJson)
    {
        if (thumbnailInfoJson.HasText())
        {
            var thumbs = new List<Thumbnail>();
            try
            {
                var info = JObject.Parse(await httpClient.GetStringAsync(thumbnailInfoJson));
                var sizes = info["sizes"] ?? new JArray();
                foreach (var size in sizes.Cast<JObject>())
                {
                    var thumb = new Thumbnail
                    {
                        Width = size.Value<int>("width"),
                        Height = size.Value<int>("height"),
                    };
                    thumb.Src = $"{thumbnailInfoJson.Replace("/info.json", "")}/full/{thumb.Width},{thumb.Height}/0/default.jpg";
                    thumbs.Add(thumb);
                }

                return thumbs;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    [HttpGet]
    [Route("reingest/{space}/{id}")]
    public async Task<ActionResult> Reingest(int space, string id)
    {
        var reingestCallContext = new DlcsCallContext("AssetController::Index-ReingestImage", $"{space}/{id}");
        var result = await dlcs.ReingestImage(space, id, reingestCallContext);
        TempData["reingest-asset"] = result?.ModelId;
        return RedirectToAction("Index", "Asset", new { space, id });
    }
    
    
    [HttpGet]
    [Route("delete/{space}/{id}")]
    public Task<ActionResult> Delete(int space, string id)
    {
        throw new NotSupportedException("To be implemented");
    }
}