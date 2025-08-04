using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utils;

namespace Wellcome.Dds.Repositories;

public class ExternalIIIFReader
{
    private readonly ILogger<ExternalIIIFReader> logger;
    private readonly HttpClient httpClient;
    private readonly DlcsOptions dlcsOptions;

    public ExternalIIIFReader(
        ILogger<ExternalIIIFReader> logger,
        HttpClient httpClient,
        IOptions<DlcsOptions> dlcsOptions)
    {
        this.dlcsOptions = dlcsOptions.Value;
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<Manifest> LoadDlcsNamedQueryManifest(string identifier)
    {
        var namedQueryManifestUri = string.Format(
            dlcsOptions.SkeletonNamedQueryTemplate!, 
            dlcsOptions.CustomerDefaultSpace, 
            WebUtility.UrlEncode(identifier));

        namedQueryManifestUri += "?ts=" + DateTime.Now.Ticks;

        logger.LogDebug("Asking DLCS for NQ Manifest {namedQueryManifestUri}", namedQueryManifestUri.LogSafe());
        var manifestStream = await httpClient.GetStreamAsync(namedQueryManifestUri);
        return manifestStream.FromJsonStream<Manifest>();
    }

    public async Task<Manifest> LoadDlcsSingleAssetManifest(string identifier)
    {
        var singleAssetManifestUri = string.Format(
            dlcsOptions.SingleAssetManifestTemplate!, dlcsOptions.CustomerDefaultSpace, identifier);

        var manifestStream = await httpClient.GetStreamAsync($"{singleAssetManifestUri}?ts={DateTime.Now.Ticks}");
        return manifestStream.FromJsonStream<Manifest>();
    }

    public async Task<List<Size>> GetThumbSizesForSingleAsset(string identifier)
    {
        try
        {
            var manifest = await LoadDlcsSingleAssetManifest(identifier);
            var thumbImageService = manifest.Items?[0]?.Thumbnail?[0]?.Service?[0] as ImageService2;
            var sizes = thumbImageService?.Sizes;
            if (sizes is { Count: > 0 })
            {
                return sizes;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to retrieve any thumbs for {identifier}", identifier);
        }

        return [];
    }
    
    
    
}