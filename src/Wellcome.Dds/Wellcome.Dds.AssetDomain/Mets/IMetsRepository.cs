using System.Collections.Generic;

namespace Wellcome.Dds.AssetDomain.Mets
{
    public interface IMetsRepository
    {
        IMetsResource Get(string identifier);

        IEnumerable<IManifestationInContext> GetAllManifestationsInContext(string identifier);
         
        int FindSequenceIndex(string identifier);
    }
}
