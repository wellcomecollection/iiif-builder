using System.Threading.Tasks;

namespace Wellcome.Dds.Auth.Web
{
    public interface IAuthenticationService
    {
        Task<AuthenticationResult> Authenticate(string username, string password);
    }
}
