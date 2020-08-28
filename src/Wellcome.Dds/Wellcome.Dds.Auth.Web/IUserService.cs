using System.Threading.Tasks;

namespace Wellcome.Dds.Auth.Web
{
    public interface IUserService
    {
        Task<UserRolesResult> GetUserRoles(string nameCredential);
    }
}
