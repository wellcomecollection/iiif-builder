using System.Collections.Generic;
using System.Linq;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model;

public class BornDigitalManifestation : IManifestation
{
    public IArchiveStorageStoredFileInfo SourceFile { get; set; }
    public string Id { get; }
    public string Label { get; }
    public string Type { get; }
    public int? Order { get; }
    public IModsData ModsData { get; }
    public IModsData ParentModsData { get; }
    public bool Partial { get; }
    public string GetRootId()
    {
        throw new System.NotImplementedException();
    }

    public List<IPhysicalFile> Sequence { get; set; }

    public List<IStoredFile> SynchronisableFiles
    {
        get
        {
            return Sequence.Select(pf => pf.Files.First()).ToList();
        }
    }

    public IStructRange RootStructRange { get; set; }
    public string[] PermittedOperations { get; }
    public string FirstInternetType { get; }
    public List<string> IgnoredStorageIdentifiers { get; }
    public IStoredFile PosterImage { get; set; }
}