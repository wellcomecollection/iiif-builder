using System;
using System.Collections.Generic;
using System.Linq;
using IIIF;
using IIIF.ImageApi;

namespace Wellcome.Dds.Repositories.Presentation;

/// <summary>
/// This class could later contribute to iiif-net
/// </summary>
public class ImageApiSizeHelper
{
    // Will add some `w,` to these
    private static readonly string[] ThumbSizes = [
        "1338,",
        "880,",
        "420,",
        "282,",
        "!1024,1024", 
        "!400,400",
        "!200,200",
        "!100,100"
    ];

    // Move the constructed sizeParam instances to a static constructor
    
    private static readonly int MaxNonOpenThumbWidth = 200;

    
    public static List<Size> GetPossibleSizes(Size actualSize, bool open)
    {
        var sizes = new List<Size>();
        foreach (var sizeParamString in ThumbSizes)
        {
            var sizeParam = SizeParameter.Parse(sizeParamString);
            if (
                sizeParam.Upscaled || 
                sizeParam.Max || 
                sizeParam.PercentScale > 0 ||
                sizeParam is { Height: > 0, Confined: false } ||
                sizeParam is { Height: 0 or null, Confined: true })
            {
                throw new NotSupportedException("Unsupported IIIF Size Parameter: " + sizeParamString);
            }

            if (sizeParam.Confined)
            {
                var requiredSize = new Size(sizeParam.Width!.Value, sizeParam.Height!.Value);
                sizes.Add(Size.Confine(requiredSize, actualSize));
            }
            else
            {
                // should only be of the form `w,` if our checks above have succeeded
                sizes.Add(Size.Resize(actualSize, targetWidth: sizeParam.Width));
            }
        }

        if (!open)
        {
            sizes = sizes.Where(s => s.Width <= MaxNonOpenThumbWidth).ToList();
        }
        return sizes.OrderByDescending(s => s.Width).ToList();
    }
}