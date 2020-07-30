namespace Wellcome.Dds.Auth.Web
{
    public interface IUserService
    {
        Roles GetUserRoles(string username, out string failureMessage);
    }
}
