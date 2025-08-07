using System.Threading.Tasks;

namespace Wellcome.Dds.Common;

public interface IIdentityService
{
    DdsIdentity GetIdentity(string s);
    Task<DdsIdentity> GetIdentityAsync(string s);
}