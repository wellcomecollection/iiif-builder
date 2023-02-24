using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain.Workflow;

public interface ICacheInvalidationPathPublisher
{
    Task<string[]> PublishInvalidation(string identifier, bool includeTextResources);
}