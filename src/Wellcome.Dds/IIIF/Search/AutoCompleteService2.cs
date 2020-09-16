using IIIF.Presentation.Content;

namespace IIIF.Search
{
    public class AutoCompleteService2 : ExternalResource, IService
    {
        public const string AutoComplete2Profile = "http://iiif.io/api/search/2/autocomplete";
        
        public AutoCompleteService2() : base(nameof(AutoCompleteService2))
        {
            // Allow callers to decide whether to set the @context
            Profile = AutoComplete2Profile;
        }
        
    }
}