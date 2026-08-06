using IIIF;
using IIIF.Presentation.V2;
using IIIF.Presentation.V3.Strings;
using Newtonsoft.Json;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories.Presentation.V2.IXIF
{
    /// <summary>
    /// Service user to for by UV for legacy P2 manifests.
    /// </summary>
    public class AccessControlHints : ResourceBase, IService
    {
        private const string IdTemplate = "https://wellcomelibrary.org/iiif/{identifier}/access-control-hints-service";
        public override string? Type { get; set; } = null;
        
        [JsonProperty(Order = 5, PropertyName = "accessHint")]
        public string AccessHint { get; }

        public AccessControlHints(DdsIdentity identifier, LanguageMap accessHint)
        {
            AccessHint = accessHint.ToString();
            Id = IdTemplate.Replace("{identifier}", identifier.Value);
            Profile = Constants.Profiles.AccessControlHints;
            Context = "http://wellcomelibrary.org/ld/iiif-ext/0/context.json";
        }
    }
}