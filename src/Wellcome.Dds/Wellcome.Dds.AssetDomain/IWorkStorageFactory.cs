using System.Threading.Tasks;

namespace Wellcome.Dds.AssetDomain
{
    /// <summary>
    /// Factory for generating <see cref="IWorkStore"/> implementations.
    /// </summary>
    public interface IWorkStorageFactory
    {
        /// <summary>
        /// Get <see cref="IWorkStore"/> for specified bNumber. 
        /// </summary>
        Task<IWorkStore> GetWorkStore(string identifier);
    }
}
