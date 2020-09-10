using System.Collections.Generic;
using System.Linq;
using IIIF.Presentation;
using IIIF.Presentation.Content;

namespace Wellcome.Dds.Repositories.Presentation
{
    public static class ProviderExtensions
    {
        public static void AddWellcomeProvider(this ResourceBase iiifResource)
        {
            var agent = new Agent
            {
                Id = "https://wellcomecollection.org",
                Label = Lang.Map("en",
                    "Wellcome Collection",
                    "183 Euston Road",
                    "London NW1 2BE",
                    "UK"),
                Homepage = new List<ExternalResource>
                {
                    new ExternalResource("Text")
                    {
                        Id = "https://wellcomecollection.org/works",
                        Label = Lang.Map("Explore our collections"),
                        Format = "text/html"
                    }
                },
                Logo = new List<Image>
                {
                    // TODO - Wellcome Collection logo
                    new Image
                    {
                        Id = "https://wellcomelibrary.org/assets/img/squarelogo64.png",
                        Format = "image/png"
                    }
                }
            };
            iiifResource.Provider ??= new List<Agent>();
            iiifResource.Provider.Add(agent);
        }
        
        
        public static void AddOtherProvider(this ResourceBase iiifResource, ManifestationMetadata manifestationMetadata, string host)
        {
            var locationOfOriginal = manifestationMetadata.Metadata
                .FirstOrDefault(m => m.Label == "Location");
            if (locationOfOriginal == null) return;
            var agent = PartnerAgents.GetAgent(locationOfOriginal.StringValue, host);
            if (agent == null) return;
            iiifResource.Provider ??= new List<Agent>();
            iiifResource.Provider.Add(agent);
        }

    }
}