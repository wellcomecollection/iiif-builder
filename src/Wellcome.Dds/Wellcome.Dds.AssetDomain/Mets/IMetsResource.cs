namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IMetsResource : IFileBasedResource
    {
        string Id { get; }
        string Label { get; }
        string Type { get; }
        int? Order { get; }
        IModsData ModsData { get; }
        IModsData ParentModsData { get; }
        bool Partial { get; }

        string GetRootId();
    }
}
