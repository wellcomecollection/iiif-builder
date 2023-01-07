using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IMetsResource : IFileBasedResource
    {
        DdsIdentifier Identifier { get; }
        string? Label { get; }
        string? Type { get; }
        int? Order { get; }
        ISectionMetadata? SectionMetadata { get; }
        ISectionMetadata? ParentSectionMetadata { get; }
        bool Partial { get; }

        string GetRootId();
    }
}
