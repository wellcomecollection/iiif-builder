using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IManifestation : IMetsResource
    {
        /// <summary>
        /// Gets or sets a collection of physical files associated with manifest (jpeg, jp2, mpeg etc)
        /// </summary>
        List<IPhysicalFile> Sequence { get; set; }
        
        /// <summary>
        /// Gets or sets a collection of physical files that are relevant for processing.
        /// </summary>
        List<IPhysicalFile> SignificantSequence { get; }
        IStructRange RootStructRange { get; set; }
        string[] PermittedOperations { get; }
        string FirstSignificantInternetType { get; }
        List<string> IgnoredStorageIdentifiers { get; }
        IStoredFile PosterImage { get; set; }
    }
}
