using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories
{
    public class Dds : IDds
    {
        private DdsOptions ddsOptions;
        private DdsContext ddsContext;
        private ILogger<Dds> logger;
        private Synchroniser synchroniser;

        public Dds(
            IOptions<DdsOptions> options, 
            DdsContext ddsContext, 
            ILogger<Dds> logger,
            Synchroniser synchroniser)
        {
            this.ddsOptions = options.Value;
            this.ddsContext = ddsContext;
            this.logger = logger;
            this.synchroniser = synchroniser;
        }

        public List<Manifestation> AutoComplete(string query)
        {
            return ddsContext.AutoComplete(query);
        }

        public List<Manifestation> GetByAssetType(string type)
        {
            return ddsContext.GetByAssetType(type);
        }

        public Dictionary<string, long> GetTotalsByAssetType()
        {
            return ddsContext.GetTotalsByAssetType();
        }

        public async Task RefreshManifestations(string id, Work work = null)
        {
            await synchroniser.RefreshDdsManifestations(id, work);
        }

        public ManifestationMetadata GetManifestationMetadata(string identifier)
        {
            var resultDdsId = new DdsIdentifier(identifier);
            var result = new ManifestationMetadata
            {
                Identifier = resultDdsId,
                Manifestations = ddsContext.Manifestations
                    .Where(fm => fm.PackageIdentifier == resultDdsId.PackageIdentifier && fm.Index >= 0)
                    .OrderBy(fm => fm.Index)
                    .ToList(),
                Metadata = ddsContext.Metadata
                    .Where(m => m.ManifestationId == resultDdsId.PackageIdentifier)
                    .ToList()
            };
            return result;
        }

        public List<Manifestation> GetManifestationsForChildren(string workReferenceNumber)
        {
            return ddsContext.Manifestations
                .Where(m => m.CalmAltRefParent == workReferenceNumber)
                .ToList();
        }

        public Manifestation GetManifestation(string id)
        {
            return ddsContext.Manifestations.Find(id);
        }
    }
}
