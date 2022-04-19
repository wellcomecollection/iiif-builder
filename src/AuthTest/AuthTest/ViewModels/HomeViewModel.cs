using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using AuthTest.Controllers;

namespace AuthTest.ViewModels
{
    public class HomeViewModel
    {
        public string Nickname { get; }
        public string EmailAddress { get; }
        public string Identifier { get; }
        public string Role { get; }
        
        public IEnumerable<string> DLCSRoles { get; }

        public HomeViewModel(IEnumerable<Claim> claims)
        {
            Identifier = GetClaim(claims, ClaimTypes.NameIdentifier);
            EmailAddress = GetClaim(claims, ClaimTypes.Email);
            Nickname = GetClaim(claims, "nickname");
            Role = GetClaim(claims, "https://wellcomecollection.org/patron_role");

            if (RoleMappings.Map.TryGetValue(Role, out var dlcsRoles))
            {
                DLCSRoles = dlcsRoles;
            }
        }

        public HomeViewModel()
        {
            throw new System.NotImplementedException();
        }

        private static string GetClaim(IEnumerable<Claim> claims, string claimType)
            => claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }
}