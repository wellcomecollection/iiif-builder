using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IManifestation : IMetsResource
    {
        /// <summary>
        /// Gets or sets a collection of physical files associated with manifest (jpeg, jp2, mpeg etc)
        /// </summary>
        List<IPhysicalFile>? Sequence { get; set; }
        List<IStoredFile>? SynchronisableFiles { get; }
        IStructRange? RootStructRange { get; set; }
        string[] PermittedOperations { get; }
        string? FirstInternetType { get; }
        List<string>? IgnoredStorageIdentifiers { get; }
        IStoredFile? PosterImage { get; set; }
        
        /// <summary>
        /// A map to look up files by their IDs
        /// </summary>
        Dictionary<string, IPhysicalFile>? PhysicalFileMap { get; set; }
    }
}
