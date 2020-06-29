namespace Wellcome.Dds.AssetDomain
{
    public interface IFileBasedResource
    {
        IStoredFileInfo SourceFile { get; set; }
    }
}
