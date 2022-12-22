
namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class AVDerivative
    {
        public AVDerivative(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; set; }
        public string Label { get; set; }
    }
}
