using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Primitives;

namespace Wellcome.Dds.Repositories.Presentation;

public class ThumbnailSizeDecorator
{
    private readonly ExternalIIIFReader externalIIIFReader;

    public ThumbnailSizeDecorator(ExternalIIIFReader externalIIIFReader)
    {
        this.externalIIIFReader = externalIIIFReader;
    }

    public async Task<List<ThumbnailSizeDecoratorResult>> UpdateManifestSizesFromExternal(
        Manifest ddsManifest,
        string manifestationIdentifier)
    {
        var result = new List<ThumbnailSizeDecoratorResult>();
        
        // Only ImageService2 for now
        var ddsThumbServices = new Dictionary<string, ImageService2>(ddsManifest.Items!.Count);
        var dlcsThumbServices = new Dictionary<string, ImageService2>(ddsManifest.Items.Count);
        Manifest dlcsManifest;
        try
        {
            dlcsManifest = await externalIIIFReader.LoadDlcsNamedQueryManifest(manifestationIdentifier);
        }
        catch (Exception e)
        {
            result.Add(new ThumbnailSizeDecoratorResult(-1, manifestationIdentifier)
            {
                Problem = $"Unable to retrieve Manifest from DLCS: {e.Message}"
            });
            return result;
        }
        for (int i = 0; i < ddsManifest.Items.Count; i++)
        {
            var ddsThumbService = ddsManifest.Items[i].Thumbnail?[0].Service?[0] as ImageService2;
            if (ddsThumbService == null)
            {
                continue;
            }

            var ddsIdPart = ddsThumbService.Id!.Split('/')[^1];
            ddsThumbServices[ddsIdPart] = ddsThumbService;

            var dlcsThumbService = dlcsManifest.Items![i].Thumbnail?[0].Service?.SingleOrDefault(svc => svc is ImageService2) as ImageService2;
            if (dlcsThumbService == null)
            {
                result.Add(new ThumbnailSizeDecoratorResult(i, ddsIdPart)
                {
                    Problem = $"No thumbnail service at index {i}"
                });
                continue;
            }
            
            var dlcsIdPart = dlcsThumbService.Id!.Split('/')[^1];
            if (ddsIdPart != dlcsIdPart)
            {
                result.Add(new ThumbnailSizeDecoratorResult(i, ddsIdPart)
                {
                    Problem = $"Different assets at index {i}: DDS has {ddsIdPart}, DLCS has {dlcsIdPart}"
                });
                continue;
            }
            
            // We know that DDS sizes are in ascending order, but DLCS are not (at least for now)
            dlcsThumbService.Sizes = dlcsThumbService.Sizes.OrderBy(s => s.Width).ToList();
            dlcsThumbServices[ddsIdPart] = dlcsThumbService;

            if (dlcsThumbService.Sizes.Count != ddsThumbService.Sizes.Count)
            {
                result.Add(new ThumbnailSizeDecoratorResult(i, ddsIdPart)
                {
                    Problem = $"Different number of sizes at index {i}",
                    ComputedSizes = ddsThumbService.Sizes,
                    GeneratedSizes = dlcsThumbService.Sizes
                });
                continue;
            }

            // now compare the actual sizes - still a success for now if they differ, but we want to log it
            var successResult = new ThumbnailSizeDecoratorResult(i, ddsIdPart)
            {
                Success = true
            };
            result.Add(successResult);
            for (int si = 0; si < ddsThumbService.Sizes.Count; si++)
            {
                var ddsSize = ddsThumbService.Sizes[si];
                var dlcsSize = dlcsThumbService.Sizes[si];
                if (!(ddsSize.Width == dlcsSize.Width && ddsSize.Height == dlcsSize.Height))
                {
                    successResult.SizesDiffer = true;
                    successResult.ComputedSizes = ddsThumbService.Sizes;
                    successResult.GeneratedSizes = dlcsThumbService.Sizes;
                    break;
                }
            }
        }

        if (result.Any(r => r.Success == false))
        {
            // One or more attempts to match thumb services failed, so don't mutate the manifest
            return result;
        }

        // Only now will we swap in the DLCS sizes in the manifest instead of the computed sizes
        foreach (var serviceResult in result.Where(r => r.SizesDiffer))
        {
            ddsThumbServices[serviceResult.AssetIdPart].Sizes = dlcsThumbServices[serviceResult.AssetIdPart].Sizes;
        }
        
        // We also need to update the manifest thumbnail, and we assume that the manifest thumbnail is one
        // of the manifest's canvases thumbnails.
        if (ddsManifest.Thumbnail is not { Count: 1 }) return result;

        if (ddsManifest.Thumbnail[0].Service?[0] is not ImageService2 manifestThumbService) return result;
        
        var manifestThumbnailIdPart = manifestThumbService.Id!.Split('/')[^1];
        manifestThumbService.Sizes = ddsThumbServices[manifestThumbnailIdPart].Sizes;

        return result;
    }

}

public class ThumbnailSizeDecoratorResult
{
    public ThumbnailSizeDecoratorResult(int ddsIndex, string assetIdPart)
    {
        DdsIndex = ddsIndex;
        AssetIdPart = assetIdPart;
    }
    
    public string AssetIdPart { get; set; }
    
    public bool Success { get; set; } = false;
    // For now we will consider it to be a Success if all assets are matched across the manifests,
    // even if the sizes don't match - we're not trusting our own computation just yet.
    public bool SizesDiffer { get; set; }
    public int DdsIndex { get; set; }
    public string? Problem { get; set; }
    
    // Only bother populating these if there is a discrepancy
    public List<Size>? ComputedSizes { get; set; }
    public List<Size>? GeneratedSizes { get; set; }

    public string GetMismatchSummary()
    {
        var sb = new StringBuilder();
        bool flag = false;
        for (int i = 0; i < ComputedSizes!.Count; i++)
        {
            if (ComputedSizes[i].Width != GeneratedSizes![i].Width ||
                ComputedSizes[i].Height != GeneratedSizes[i].Height)
            {
                if (flag) sb.Append("; ");
                sb.Append($"expected ({ComputedSizes[i].Width},{ComputedSizes[i].Height}), ");
                sb.Append($"actual ({GeneratedSizes[i].Width},{GeneratedSizes[i].Height})");
                flag = true;
            }
        }

        return sb.ToString();
    }
}