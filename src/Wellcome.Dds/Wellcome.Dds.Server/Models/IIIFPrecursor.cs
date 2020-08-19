using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.Catalogue;

namespace Wellcome.Dds.Server.Models
{
    public class IIIFPrecursor
    {
        public string Id { get; set; }
        public string Comment { get; set; }
        public string Label { get; set; }
        public Work CatalogueMetadata { get; set; }
        public DigitisedManifestation ManifestSource { get; set; }
        public SimpleCollectionModel SimpleCollectionSource { get; set; }
        public string Pdf { get; set; }
        public string IIIFVersion { get; set; }
    }
}