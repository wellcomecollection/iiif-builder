using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.Dashboard.Models;

public class AssetModel
{
    public string ModelId { get; set; }
    public int Space { get; set; }
    public Image Asset { get; set; }
    public string DlcsPortalPage { get; set; }
    public List<Thumbnail> Thumbnails { get; set; }
}