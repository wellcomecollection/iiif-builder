
namespace Wellcome.Dds.AssetDomain.Dlcs.Model
{
    public class ErrorByMetadata
    {
        public string? MetadataString { get; set; }
        public long MetadataNumber { get; set; }
        public int Count { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }

        public override string ToString()
        {
            return string.Format("[Errors: {0}, MetadataString: {1}, MetadataNumber: {2}", 
                Count, MetadataString, MetadataNumber);
        }
    }
}
