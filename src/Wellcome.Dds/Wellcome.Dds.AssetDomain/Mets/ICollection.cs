using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface ICollection : IMetsResource
    {
        List<ICollection>? Collections { get; }
        List<IManifestation>? Manifestations { get; }
    }
}
