using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Utils;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories
{
    public class Dds : IDds
    {
        private readonly DdsContext ddsContext;
        private readonly Synchroniser synchroniser;

        public Dds(DdsContext ddsContext,
            Synchroniser synchroniser)
        {
            this.ddsContext = ddsContext;
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

        public async Task RefreshManifestations(DdsIdentifier ddsId, Work? work = null)
        {
            await synchroniser.RefreshDdsManifestations(ddsId, work);
        }

        public ManifestationMetadata GetManifestationMetadata(string identifier)
        {
            var resultDdsId = new DdsIdentifier(identifier);
            var result = new ManifestationMetadata
            (
                identifier: resultDdsId,
                manifestations: ddsContext.Manifestations
                    .Where(fm => fm.PackageIdentifier == resultDdsId.PackageIdentifier && fm.Index >= 0)
                    .OrderBy(fm => fm.Index)
                    .ToList(),
                metadata: ddsContext.Metadata
                    .Where(m => m.ManifestationId == resultDdsId.PackageIdentifier)
                    .ToList()
            );
            return result;
        }

        public List<Manifestation> GetManifestationsForChildren(string? workReferenceNumber)
        {
            if (workReferenceNumber.HasText())
            {
                return ddsContext.Manifestations
                    .Where(m => m.CalmAltRefParent == workReferenceNumber)
                    .ToList();
            }

            return new List<Manifestation>(0);
        }

        public Manifestation? GetManifestation(string id)
        {
            return ddsContext.Manifestations.Find(id);
        }
    }
}
