using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using Utils;

namespace Wellcome.Dds.Repositories.Presentation
{
    public static class ProviderExtensions
    {
        public static void AddWellcomeProvider(this ResourceBase iiifResource, string host)
        {
            var agent = new Agent
            {
                Id = Constants.WellcomeCollectionUri,
                Label = Lang.Map("en",
                    Constants.WellcomeCollection,
                    "183 Euston Road",
                    "London NW1 2BE UK",
                    "T +44 (0)20 7611 8722",
                    "E library@wellcomecollection.org",
                    Constants.WellcomeCollectionUri),
                Homepage = new List<ExternalResource>
                {
                    new("Text")
                    {
                        Id = Constants.WellcomeCollectionUri,
                        Label = Lang.Map("Wellcome Collection"),
                        Format = "text/html"
                    }
                },
                Logo = new List<Image>
                {
                    // TODO - Wellcome Collection logo
                    new()
                    {
                        Id = $"{host}/logos/wellcome-collection-black.png",
                        Format = "image/png"
                    }
                }
            };
            iiifResource.Provider ??= new List<Agent>();
            iiifResource.Provider.Add(agent);
        }
        
        
        public static void AddOtherProvider(this ResourceBase iiifResource, ManifestationMetadata manifestationMetadata, string host)
        {
            var locationOfOriginal = manifestationMetadata.Metadata.GetLocationOfOriginal();
            if (!locationOfOriginal.HasText()) return;
            var agent = PartnerAgents.GetAgent(locationOfOriginal, host);
            if (agent == null) return;
            iiifResource.Provider ??= new List<Agent>();
            iiifResource.Provider.Add(agent);
        }

    }
}