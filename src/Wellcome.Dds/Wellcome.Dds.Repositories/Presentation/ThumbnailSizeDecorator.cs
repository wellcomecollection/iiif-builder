using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Wellcome.Dds.Repositories.Presentation;

public class ThumbnailSizeDecorator
{
    private readonly ExternalIIIFReader externalIIIFReader;

    public ThumbnailSizeDecorator(ExternalIIIFReader externalIIIFReader)
    {
        this.externalIIIFReader = externalIIIFReader;
    }

    public async Task<List<ThumbnailSizeDecoratorResult>> UpdateManifestSizesFromExternal(Manifest ddsManifest,
        string manifestationIdentifier, ILogger<IIIFBuilder> logger)
    {
        var result = new List<ThumbnailSizeDecoratorResult>();
        
        // Only ImageService2 for now
        // var ddsThumbServices = new Dictionary<string, ImageService2>(ddsManifest.Items!.Count);
        // var dlcsThumbServices = new Dictionary<string, ImageService2>(ddsManifest.Items.Count);
        var ddsCanvases = new Dictionary<string, Canvas>(ddsManifest.Items!.Count);
        var dlcsCanvases = new Dictionary<string, Canvas>(ddsManifest.Items.Count);
        
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
                // usually because it's not an image!
                continue;
            }

            var ddsIdPart = ddsThumbService.Id!.Split('/')[^1];
            
            
            //ddsThumbServices[ddsIdPart] = ddsThumbService;
            ddsCanvases[ddsIdPart] = ddsManifest.Items[i];
            
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
            //dlcsThumbServices[ddsIdPart] = dlcsThumbService;
            dlcsCanvases[ddsIdPart] = dlcsManifest.Items[i];

            // Ideally, we would report a problem with the whole manifest here, but we want to be tolerant
            // of a low error rate, so we won't reject this - we just won't _use_ it.
            var successResult = new ThumbnailSizeDecoratorResult(i, ddsIdPart)
            {
                Success = true
            };
            result.Add(successResult);
            
            
            if (dlcsThumbService.Sizes.Count != ddsThumbService.Sizes.Count)
            {
                successResult.CountsDiffer = true;
                continue; // i.e., don't see if SizesDiffer
                
                // Put this back in when we have a super-reliable engine-thumbs
                // result.Add(new ThumbnailSizeDecoratorResult(i, ddsIdPart)
                // {
                //     Problem = $"Different number of sizes at index {i}",
                //     ComputedSizes = ddsThumbService.Sizes,
                //     GeneratedSizes = dlcsThumbService.Sizes
                // });
                // continue;
            }

            // now compare the actual sizes - still a success for now if they differ, but we want to log it
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
            bool canvasesMissing = true;
            if (!ddsCanvases.ContainsKey(serviceResult.AssetIdPart))
            {
                logger.LogError("ddsCanvases does not contain key {assetIdPart}.", serviceResult.AssetIdPart);
                canvasesMissing = false;
            }
            if (!dlcsCanvases.ContainsKey(serviceResult.AssetIdPart))
            {
                logger.LogError("dlcsCanvases does not contain key {assetIdPart}.", serviceResult.AssetIdPart);
                canvasesMissing = false;
            }

            if (canvasesMissing)
            {
                throw new InvalidOperationException("Missing canvas: " + serviceResult.AssetIdPart);
            }
            var ddsCanvas = ddsCanvases[serviceResult.AssetIdPart];
            var dlcsCanvas = dlcsCanvases[serviceResult.AssetIdPart];
            var ddsThumbService = ddsCanvas.Thumbnail?[0].Service?[0] as ImageService2;
            var dlcsThumbService = dlcsCanvas.Thumbnail?[0].Service?.SingleOrDefault(svc => svc is ImageService2) as ImageService2;
            if (ddsThumbService != null && dlcsThumbService != null)
            {
                // swap the sizes
                if (ddsCanvas.Thumbnail?[0] is Image img && dlcsThumbService.Sizes.Count > 0)   
                {
                    ddsThumbService.Sizes = dlcsThumbService.Sizes.OrderBy(s => s.Width).ToList();
                    var smallest = ddsThumbService.Sizes[0];
                    var largest = ddsThumbService.Sizes[^1];
                    img.Width = smallest.Width;
                    img.Height = smallest.Height;
                    img.Id = $"{ddsThumbService.Id}/full/{smallest.Width},{smallest.Height}/0/default.jpg";
                    
                    // The service itself has a width and height, from the largest thumb
                    ddsThumbService.Width = largest.Width;
                    ddsThumbService.Height = largest.Height;
                    
                    // We also need to update the Canvas's painting annotation
                    var paintingAnno = ddsCanvas.Items?[0].Items?[0] as PaintingAnnotation;
                    if (paintingAnno?.Body is Image cvsImg)
                    {
                        cvsImg.Width = largest.Width;
                        cvsImg.Height = largest.Height;
                        cvsImg.Id = $"{cvsImg.Service?[0].Id}/full/{largest.Width},{largest.Height}/0/default.jpg";
                    }
                }
            }
        }
        
        // We also need to update the manifest thumbnail, and we assume that the manifest thumbnail is one
        // of the manifest's canvases thumbnails.
        if (ddsManifest.Thumbnail is not { Count: 1 }) return result;
        if (ddsManifest.Thumbnail[0].Service?[0] is not ImageService2 manifestThumbService) return result;
        
        var manifestThumbnailIdPart = manifestThumbService.Id!.Split('/')[^1];
        ddsManifest.Thumbnail = ddsCanvases[manifestThumbnailIdPart].Thumbnail;

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
    public bool CountsDiffer { get; set; }
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