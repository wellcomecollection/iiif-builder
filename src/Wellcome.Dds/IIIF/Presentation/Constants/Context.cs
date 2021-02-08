using System;
using System.Collections.Generic;

namespace IIIF.Presentation.Constants
{
    public static class Context
    {
        public const string Presentation3Context = "http://iiif.io/api/presentation/3/context.json";
        public const string Presentation2Context = "http://iiif.io/api/presentation/2/context.json";

        public static void EnsurePresentation3Context(this JsonLdBase resource)
        {
            resource.EnsureContext(Presentation3Context);
        }
        
        public static void EnsurePresentation2Context(this JsonLdBase resource)
        {
            resource.EnsureContext(Presentation2Context);
        }

        // The IIIF context must be last in the list, to override any that come before it.
        public static void EnsureContext(this JsonLdBase resource, string contextToEnsure)
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
            bool hasPresentation2Context = false;
            foreach (string workingContext in workingContexts)
            {
                if (workingContext == Presentation3Context)
                {
                    hasPresentation3Context = true;
                }
                else if (workingContext == Presentation2Context)
                {
                    hasPresentation2Context = true;
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

            if (hasPresentation2Context && hasPresentation3Context)
            {
                throw new InvalidOperationException(
                    "You cannot have Presentation 2 and Presentation 3 contexts in the same resource.");
            }
            // These have to come last
            if (hasPresentation3Context)
            {
                newContexts.Add(Presentation3Context);
            }
            if (hasPresentation2Context)
            {
                newContexts.Add(Presentation2Context);
            }

            // Now JSON-LD rules. The @context is the only Presentation 3 element that has this.
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
