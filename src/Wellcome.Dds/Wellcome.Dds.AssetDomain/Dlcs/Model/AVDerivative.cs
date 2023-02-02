
namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class AVDerivative
    {
        public AVDerivative(string publicUrl, string dlcsUrl, string label)
        {
            PublicUrl = publicUrl;
            DlcsUrl = dlcsUrl;
            Label = label;
        }

        public string PublicUrl { get; set; }
        public string DlcsUrl { get; set; }
        public string Label { get; set; }
    }
}
