using System;
using System.Collections.Generic;
using System.Linq;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model;

public class BornDigitalManifestation : IManifestation
{
    public IArchiveStorageStoredFileInfo SourceFile { get; set; }
    public string Id { get; set; }
    public string Label { get; set; }
    public string Type { get; set; }
    public int? Order { get; set; }
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
    
    // Going to assume that this isn't relevant any more - or will need a whole new set of operations.
    // This was originally for UV - allow download etc, allow crop,...
    public string[] PermittedOperations => Array.Empty<string>();
    public string FirstInternetType { get; }
    public List<string> IgnoredStorageIdentifiers { get; set; }
    public IStoredFile PosterImage { get; set; }
}