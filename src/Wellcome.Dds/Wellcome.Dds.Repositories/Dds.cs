using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        public List<Manifestation> AutoComplete(string id)
        {
            return ddsContext.AutoComplete(id);
        }

        public List<Manifestation> GetByAssetType(string type)
        {
            return ddsContext.GetByAssetType(type);
        }

        public Dictionary<string, int> GetTotalsByAssetType()
        {
            return ddsContext.GetTotalsByAssetType();
        }

        public async Task RefreshManifestations(string id)
        {
            await synchroniser.RefreshFlatManifestations(id);
        }
    }
}
