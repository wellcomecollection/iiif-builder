using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain
{
    public interface IWorkStorageFactory
    {
        Task<IWorkStore> GetWorkStore(string identifier);
    }
}
