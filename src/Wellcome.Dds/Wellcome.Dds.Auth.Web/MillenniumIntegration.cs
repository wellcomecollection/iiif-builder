using System;
using System.Collections.Generic;
using System.Text;
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
                var authenticationResult = await authenticationService.Authenticate(username, password);
                if (authenticationResult.Success)
                {
                    userRoles = await userService.GetUserRoles(username);
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
                Roles = userRoles.Roles,
                Message = message,
                Success = message == "Success"
            };
            return result;
        }
    }
}