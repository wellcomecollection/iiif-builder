using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dlcs.Model;

namespace Wellcome.Dds.Dashboard.TagHelpers
{
    [HtmlTargetElement("iiif-thumb", TagStructure = TagStructure.WithoutEndTag)]
    public class DlcsIIIFThumbnailTagHelper : TagHelper
    {
        public Thumbnail SmallThumb { get; set; }
        public Thumbnail LargeThumb { get; set; }
        public string FullIIIFService { get; set; }
        public string Title { get; set; }
        
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "img";
            output.Attributes.SetAttribute("width", SmallThumb.Width);
            output.Attributes.SetAttribute("height", SmallThumb.Height);
            output.Attributes.SetAttribute("data-src", SmallThumb.Src);
            output.Attributes.SetAttribute("data-placement", "auto");
            output.Attributes.SetAttribute("data-iiif", FullIIIFService);
            string title =
                $"{Title}<br/><img src='{LargeThumb.Src}' width={LargeThumb.Width} height={LargeThumb.Height} />";
            output.Attributes.SetAttribute("title", title);
        }
    }
}