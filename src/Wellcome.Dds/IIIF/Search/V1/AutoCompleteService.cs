using System;
using IIIF.Presentation.V2;

namespace IIIF.Search.V1
{
    public class AutoCompleteService : ResourceBase, IService
    {
        public const string AutoCompleteService1Profile = "http://iiif.io/api/search/1/autocomplete";

        public override string Type
        {
            get => nameof(AutoCompleteService);
            set => throw new NotImplementedException();
        }
    }
}
