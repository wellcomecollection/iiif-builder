using System.Threading.Tasks;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.IIIF;

namespace Wellcome.Dds.Repositories.Presentation
{
    public class IIIFBuilder : IIIIFBuilder
    {
        private readonly IDashboardRepository dashboardRepository;
        private readonly ICatalogue catalogue;
        private readonly DlcsOptions dlcsOptions;
        public Task<BuildResult> Build(string identifier)
        {
            throw new System.NotImplementedException();
        }
    }
}