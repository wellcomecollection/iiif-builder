using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Repositories
{
    public class Dds : IDds
    {
        private DdsOptions ddsOptions;
        private DdsContext ddsContext;
        private ILogger<Dds> logger;

        public Dds(
            IOptions<DdsOptions> options, 
            DdsContext ddsContext, 
            ILogger<Dds> logger)
        {
            this.ddsOptions = options.Value;
            this.ddsContext = ddsContext;
            this.logger = logger;
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
    }
}
