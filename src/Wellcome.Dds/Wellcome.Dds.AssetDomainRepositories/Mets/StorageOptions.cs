namespace Wellcome.Dds.AssetDomainRepositories.Mets
{
    public class StorageOptions
    {
        public string? StorageApiTemplate { get; set; }
        public string? TokenEndPoint { get; set; }
        public string? Scope { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? StorageApiTemplateIngest { get; set; }
        public string? ScopeIngest { get; set; }
        public bool PreferCachedStorageMap { get; set; }
        public int MaxAgeStorageMap { get; set; }
    }
}
