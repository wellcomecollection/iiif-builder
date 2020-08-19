using System.Collections.Generic;
using Utils;
using Wellcome.Dds.AssetDomain.Dashboard;

namespace Wellcome.Dds.Repositories.Presentation
{
    /// <summary>
    /// This is temporarily copied from dashboard, just to have something to look at for collections.
    /// </summary>
    public class SimpleCollectionModel
    {
        public List<SimpleLink> Manifestations { get; set; }
        public List<SimpleLink> Collections { get; set; }
        
        public static SimpleCollectionModel MakeSimpleCollectionModel(IDigitisedCollection collection)
        {
            if (collection == null)
            {
                return null;
            }
            var simpleCollection = new SimpleCollectionModel();
            if (collection.Collections.HasItems())
            {
                simpleCollection.Collections = new List<SimpleLink>();
                foreach (var coll in collection.Collections)
                {
                    simpleCollection.Collections.Add(new SimpleLink
                    {
                        Label = coll.Identifier + ": " + coll.MetsCollection.Label,
                        Url = $"/presentation/{coll.Identifier}"
                    });
                }
            }
            if (collection.Manifestations.HasItems())
            {
                simpleCollection.Manifestations = new List<SimpleLink>();
                foreach (var manif in collection.Manifestations)
                {
                    simpleCollection.Manifestations.Add(new SimpleLink
                    {
                        Label = manif.Identifier + ": " + manif.MetsManifestation.Label,
                        Url = $"/presentation/{manif.Identifier}"
                    });
                }
            }
            return simpleCollection;
        }
    }
    
    public class SimpleLink
    {
        public string Label { get; set; }
        public string Url { get; set; }
    }
}