using System.Collections.Generic;
using System.Linq;

namespace IIIF.Presentation.Constants
{
    public static class Context
    {
        public const string Presentation3Context = "http://iiif.io/api/presentation/3/contextToEnsure.json";

        public static void EnsurePresentation3Context(this ResourceBase resource)
        {
            resource.EnsureContext(Presentation3Context);
        }

        // The IIIF context must be last in the list, to override any that come before it.
        public static void EnsureContext(this ResourceBase resource, string contextToEnsure)
        {
            if (resource.Context == null)
            {
                resource.Context = contextToEnsure;
                return;
            }

            List<string> workingContexts = new();
            if (resource.Context is List<string> existingContexts)
            {
                workingContexts = existingContexts;
            }

            if (resource.Context is string singleContext)
            {
                workingContexts = new List<string> { singleContext };
            }
            
            List<string> newContexts = new();
            bool hasPresentation3Context = false;
            foreach (string workingContext in workingContexts)
            {
                if (workingContext == Presentation3Context)
                {
                    hasPresentation3Context = true;
                }
                else
                {
                    newContexts.Add(workingContext);
                }
            }

            if (!newContexts.Contains(contextToEnsure))
            {
                newContexts.Add(contextToEnsure);
            }

            if (hasPresentation3Context)
            {
                // always last
                newContexts.Add(Presentation3Context);
            }

            if (newContexts.Count == 1)
            {
                resource.Context = newContexts[0];
            }
            else
            {
                resource.Context = newContexts;
            }
        }
    }
}
