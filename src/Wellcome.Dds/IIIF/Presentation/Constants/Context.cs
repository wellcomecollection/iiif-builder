using System.Collections.Generic;
using System.Linq;

namespace IIIF.Presentation.Constants
{
    public static class Context
    {
        public const string Presentation3Context = "http://iiif.io/api/presentation/3/context.json";

        public static void AddPresentation3Context(this ResourceBase resource, params string[] additionalContexts)
        {
            if (additionalContexts != null && additionalContexts.Length > 0)
            {
                resource.Context = new List<string>(additionalContexts) {Presentation3Context};
            }
            else
            {
                resource.Context = Presentation3Context;
            }
        }
    }
}
