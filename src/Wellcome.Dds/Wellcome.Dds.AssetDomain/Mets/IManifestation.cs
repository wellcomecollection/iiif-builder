using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IManifestation : IMetsResource
    {
        List<IPhysicalFile> Sequence { get; set; }
        List<IPhysicalFile> SignificantSequence { get; }
        IStructRange RootStructRange { get; set; }
        string[] PermittedOperations { get; }
        string FirstSignificantInternetType { get; }
        List<string> IgnoredStorageIdentifiers { get; }
        IStoredFile PosterImage { get; set; }
    }
}
