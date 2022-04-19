using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using AuthTest.Models;
using AuthTest.ViewModels;

namespace AuthTest.Controllers
{
    public class HomeController : Controller
    {
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index()
        {
            if (User.Identity is not { IsAuthenticated: true })
            {
                return RedirectToAction("Login", "Account");
            }
            
            return View(new HomeViewModel(User.Claims));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("mappings")]
        public IActionResult Mappings()
        {
            return View();
        }
    }

    public static class RoleMappings
    {
        // Mapping of Auth0 'role' claim : DLCS role
        public static Dictionary<string, IEnumerable<string>> Map = new()
        {
            ["Reader"] = new[] { "https://api.dlcs.io/customers/2/roles/clickthrough" },
            ["Staff"] = new[]
            {
                "https://api.dlcs.io/customers/2/roles/clickthrough",
                "https://api.dlcs.io/customers/2/roles/clinicalImages",
                "https://api.dlcs.io/customers/2/roles/restrictedFiles"
            },
            ["SelfRegistered"] = Enumerable.Empty<string>(),
            ["Excluded"] = Enumerable.Empty<string>()
        };
    }
}