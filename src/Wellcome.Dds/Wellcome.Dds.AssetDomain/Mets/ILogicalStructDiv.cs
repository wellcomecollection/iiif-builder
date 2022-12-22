using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface ILogicalStructDiv
    {
        IWorkStore WorkStore { get; }
        bool IsCollection { get; }
        bool IsManifestation { get; }
        string Id { get; set; }
        string ExternalId { get; set; }
        string ContainingFileRelativePath { get; set; }
        string AdmId { get; set; }
        string DmdId { get; set; }
        string RelativeLinkPath { get; set; }
        string LinkId { get; set; }
        string Label { get; set; }
        string Type { get; set; }
        int? Order { get; set; }
        List<ILogicalStructDiv> Children { get; set; }
        bool HasChildLink();
        ISectionMetadata? GetSectionMetadata();
        List<IPhysicalFile> GetPhysicalFiles();
        IStoredFile GetPosterImage();
    }
}
