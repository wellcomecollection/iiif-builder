using System.Threading.Tasks;

namespace Wellcome.Dds.Auth.Web.Sierra
{
    public class AllowAllAuthenticator : IAuthenticationService
    {
        public Task<AuthenticationResult> Authenticate(string username, string password)
        {
            return Task.FromResult(new AuthenticationResult() { Success = true });
        }
    }
}
