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
            // We should have ended up with a Collection, comprising more than one Manifest.
            // Each Manifest will have a copy number.
            // There might be more than one volume per copy, in which case we have
            // a nested collection.
            if (!(buildResults.First().IIIFResource is Collection bNumberCollection))
            {
                throw new IIIFBuildStateException("State is missing the parent collection");
            }

            SetCollectionRights(bNumberCollection, buildResults);
        }
        
        private static void SetCollectionRights(Collection collection, MultipleBuildResult buildResults)
        {
            if (!string.IsNullOrEmpty(collection!.Rights)) return;
            
            var rights = buildResults
                .Where(br => br.IIIFResource != collection)
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

            collection.Rights = rights.Single();
        }
    }
}