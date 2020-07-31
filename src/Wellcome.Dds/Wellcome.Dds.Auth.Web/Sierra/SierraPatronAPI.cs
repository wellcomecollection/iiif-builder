using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wellcome.Dds.Auth.Web.Sierra
{
    public class SierraPatronAPI : IUserService
    {
        public Task<UserRolesResult> GetUserRoles(string username)
        {
            string millenniumVersion = "b" + username;
            return GetUserInternal(millenniumVersion);
        }

        private Task<UserRolesResult> GetUserInternal(string millenniumVersion)
        {
            throw new NotImplementedException();
        }
    }
}
