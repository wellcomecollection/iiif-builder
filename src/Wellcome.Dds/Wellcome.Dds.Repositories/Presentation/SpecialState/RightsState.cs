using System;
using System.Linq;
using IIIF.Presentation.V3;
using Utils;
using Wellcome.Dds.IIIFBuilding;

namespace Wellcome.Dds.Repositories.Presentation.SpecialState
{
    /// <summary>
    /// State for ensuring main Collection has rights set based on child Manifests
    /// </summary>
    public class RightsState
    {
        public static void ProcessState(MultipleBuildResult buildResults)
        {
            // If processing a collection and it has no Rights, look at child manifests for rights
            var firstResult = buildResults.First().IIIFResource;
            
            // Verify that we have a Collection buildResult..
            if (firstResult is not Collection bNumberCollection)
            {
                // Unless it is AV, in which case it's fine as the original Collection is changed to manifest 
                // containing AV + PDF
                if (firstResult is Manifest manifest && manifest.ContainsAV())
                {
                    return;
                }
                
                throw new IIIFBuildStateException("State is missing the parent collection");
            }

            SetCollectionRights(bNumberCollection, buildResults);
        }
        
        private static void SetCollectionRights(Collection collection, MultipleBuildResult buildResults)
        {
            if (!string.IsNullOrEmpty(collection!.Rights)) return;
            
            var rights = buildResults
                .Where(br => br.IIIFResource is Manifest)
                .Select(i => (i.IIIFResource as ResourceBase)!.Rights)
                .Where(r => r.HasText())
                .Distinct()
                .ToList();
            if (rights.Count > 1)
            {
                // safety check - could be achieved with .Single() above but wouldn't be as clear
                throw new InvalidOperationException(
                    $"Collection has manifests with multiple differing rights: {string.Join(",", rights)}");
            }

            collection.Rights = rights.SingleOrDefault();
        }
    }
}