using System;
using System.Threading.Tasks;
using Utils;

namespace Wellcome.Dds.Auth.Web
{
    public class MillenniumIntegration
    {
        private readonly IAuthenticationService authenticationService;
        private readonly IUserService userService; // formerly IUserManagement in old DDS

        public MillenniumIntegration(
             IAuthenticationService authenticationService,
             IUserService userService
        )
        {
            this.authenticationService = authenticationService;
            this.userService = userService;
        }

        public async Task<LoginResult> LoginWithMillenniumAsync(string username, string password)
        {
            string message = null;
            UserRolesResult userRoles = null;
            if (StringUtils.AllHaveText(username, password))
            {
                // TODO - come back to the following code once we have Sierra 5.2 running.
                // - Get userRoles by the supplied username, where this might be either a username or a barcode
                // - then validate the password using the barcode or username, whichever 5.2 expects here
                // (we have options to do both via config)
                // If you _need_ to user barcode(s), they will be in userRoles.Roles.BarCodes
                userRoles = await userService.GetUserRoles(username);
                var authenticationResult = await authenticationService.Authenticate(username, password);
                if (authenticationResult.Success)
                {
                    if (userRoles.Success)
                    {
                        if (userRoles.Roles.Expires > DateTime.Now.AddDays(-1))
                        {
                            message = "Success";
                        }
                        else
                        {
                            message = "Your account expired on " + userRoles.Roles.Expires;
                        }
                    }
                }
                else
                {
                    message = authenticationResult.Message;
                }
                if (!message.HasText())
                {
                    message = "Unable to log in";
                }
            }
            else
            {
                message = "Please supply credentials!";
            }
            var result = new LoginResult
            {
                Roles = userRoles?.Roles,
                Message = message,
                Success = message == "Success"
            };
            return result;
        }
    }
}