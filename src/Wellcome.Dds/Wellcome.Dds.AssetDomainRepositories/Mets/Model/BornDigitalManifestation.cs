using System;
using System.Collections.Generic;
using System.Linq;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.AssetDomainRepositories.Mets.Model;

public class BornDigitalManifestation : IManifestation
{
    public IArchiveStorageStoredFileInfo? SourceFile { get; set; }
    public DdsIdentifier? Identifier { get; set; }
    public string? Label { get; set; }
    public string? Type { get; set; }
    public int? Order { get; set; }
    public ISectionMetadata? SectionMetadata { get; set; }
    public ISectionMetadata? ParentSectionMetadata { get; }
    public bool Partial => false; // never partial  
    
    public string GetRootId()
    {
        // We only have one manifestation per identifier for born digital
        return Identifier!.ToString();
    }

    public List<IPhysicalFile>? Sequence { get; set; }

    public List<IStoredFile> SynchronisableFiles
    {
        get
        {
            return Sequence!.Select(pf => pf.Files!.First()).ToList();
        }
    }

    public IStructRange? RootStructRange { get; set; }
    
    // Going to assume that this isn't relevant any more - or will need a whole new set of operations.
    // This was originally for UV - allow download etc, allow crop,...
    public string[] PermittedOperations => Array.Empty<string>();

    // This is not very useful for born digital items, where a sequence can be mixed media
    public string? FirstInternetType => Sequence!.First().MimeType;

    public List<string>? IgnoredStorageIdentifiers { get; set; }
    public IStoredFile? PosterImage { get; set; }
    
    public Dictionary<string, IPhysicalFile>? PhysicalFileMap { get; set; }
}