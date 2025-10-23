using System.Threading.Tasks;

namespace Wellcome.Dds.Common;

public interface IIdentityService
{
    DdsIdentity GetIdentity(string s);
    DdsIdentity GetIdentity(string s, string? generator);
}