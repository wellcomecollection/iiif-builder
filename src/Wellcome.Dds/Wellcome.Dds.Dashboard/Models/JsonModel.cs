using Wellcome.Dds.AssetDomainRepositories.Mets;

namespace Wellcome.Dds.Dashboard.Models
{
    public class JsonModel
    {
        public string BNumber { get; set; }
        public string JsonAsString { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class StorageMapModel
    {
        public string BNumber { get; set; }
        public WellcomeBagAwareArchiveStorageMap StorageMap { get; set; }
        public string PathToResolve { get; set; }
        public string ResolvedAwsKey { get; set; }
        public string ErrorMessage { get; set; }
    }
}