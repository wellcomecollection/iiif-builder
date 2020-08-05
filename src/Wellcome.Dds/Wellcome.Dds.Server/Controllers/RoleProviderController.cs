using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Utils;
using Utils.Web;
using Wellcome.Dds.Auth.Web;
using Wellcome.Dds.Server.Auth;
using Wellcome.Dds.Server.Infrastructure;
using Wellcome.Dds.Server.Models;

namespace Wellcome.Dds.Server.Controllers
{
    /// <summary>
    /// The DLCS delegates auth to the DDS by redirecting end-user requests to the login page here,
    /// and calling API here to acquire roles.
    /// 
    /// These operations were on IIIFController in Old DDS.
    /// </summary>
    [MiddlewareFilter(typeof(SessionPipeline))]
    public class RoleProviderController : Controller
    {
        private readonly MillenniumIntegration millenniumIntegration;
        private readonly IDistributedCache distributedCache;

        public RoleProviderController(
            MillenniumIntegration millenniumIntegration,
            IDistributedCache distributedCache
        )
        {
            this.millenniumIntegration = millenniumIntegration;
            this.distributedCache = distributedCache;
        }

        // Move these to config later
        private const string dlcsReturnUrl = "https://dlcs.io/auth/2/fromcas";
        private const string testReturnUrl = "/roleprovider/info";
        private const string sessionFlagKey = "roles_acquired";

        /// <summary>
        /// DLCS delegated login screen redirects to here. 
        /// This is hard-coded to redirect to https://dlcs.io/auth/2/fromcas?token=xyz 
        /// after authenticating at CAS.
        /// 
        /// All we need for a userSession are:
        /// A Bearer Token
        /// Roles
        /// 
        /// We pass the bearer token to the DLCS and it uses it out of band to acquire the roles, so we can't use session state for that.
        /// Do we even need session state? Yes, because we don't bother setting our own cookies for this. Session is simplest.
        /// But limit it to this controller only - no need to have session anywhere else.
        /// And make sure session cookies don't have cache implications, e.g., Cloudfront
        /// 
        /// Can we use the session ID as the DDS bearer token? Would that be leaking something? 
        /// I don't think so? It isn't exposed in the DLCS-client interaction (DLCS mints a different token that it manages)
        /// 
        /// HttpContext.Session.Id
        /// 
        /// This is very simple, but is it safe?
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> DlcsLogin()
        {
            return await Login(dlcsReturnUrl);
        }

        public async Task<ActionResult> TestLogin()
        {
            return await Login(testReturnUrl);
        }

        public ActionResult Info([FromQuery] string token)
        {
            var info = new RoleProviderInfoModel();
            if (token.HasText())
            {
                info.SuppliedToken = token;
                var storedRoles = distributedCache.GetString(GetRolesKey(token));
                if (storedRoles.HasText())
                {
                    info.RolesFromToken = new Roles(storedRoles);
                }
            }

            var session = HttpContext.Session;
            var sessionFlag = session.GetInt32(sessionFlagKey) ?? 0;
            info.SessionFlag = sessionFlag;

            // In this info action, we'll attempt to read cached roles regardless of "login state"
            var storedRoleString = distributedCache.GetString(GetRolesKey(session));
            if (storedRoleString.HasText())
            {
                info.RolesFromSession = new Roles(storedRoleString);
            }

            return View(info);
        }

        /// <summary>
        /// This is what the DLCS calls out of band using its secret knowledge
        /// </summary>
        /// <returns></returns>
        [Authorize(AuthenticationSchemes = BasicAuthenticationDefaults.AuthenticationScheme)]
        public ActionResult RolesForToken([FromQuery] string token)
        {
            var storedRoles = distributedCache.GetString(GetRolesKey(token));
            if (storedRoles.HasText())
            {
                var sierraRoles = new Roles(storedRoles);
                return Json(sierraRoles.GetDlcsRoles());
            }

            return NotFound($"Could not find user for token '{token}'");
        }
        
        private async Task<ActionResult> Login(string returnUrl)
        {            
            var session = HttpContext.Session;
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.AppendStandardNoCacheHeaders();
            string username = null;
            string password = null;
            if (Request.Method == "POST")
            {
                username = Request.Form["directlogin_u"].FirstOrDefault();
                password = Request.Form["directlogin_p"].FirstOrDefault();
            }
            LoginResult loginResult = null;
            if (username.HasText())
            {
                loginResult = await millenniumIntegration.LoginWithMillenniumAsync(username, password);
                if (loginResult.Success)
                {
                    session.SetInt32(sessionFlagKey, 1);
                    await distributedCache.SetStringAsync(GetRolesKey(session), loginResult.Roles.ToString());
                    return new RedirectResult(returnUrl + "?token=" + session.Id);
                }
            }
            var sessionFlag = session.GetInt32(sessionFlagKey) ?? 0;
            if (sessionFlag == 1)
            {
                // The user has logged in; did we store their roles?
                var storedRoleString = await distributedCache.GetStringAsync(GetRolesKey(session));
                if (storedRoleString.HasText())
                {
                    return new RedirectResult($"{returnUrl}?token={session.Id}");
                }
                // We recognised the session, but don't have roles stored that the DLCS could retrieve
                session.SetInt32(sessionFlagKey, 0);
            }
            var model = new LoginModel
            {
                Username = username,
                Message = loginResult?.Message
            };
            return View("OpenedLoginWindow", model);
        }

        private string GetRolesKey(ISession session) => GetRolesKey(session.Id);

        private string GetRolesKey(string token) => $"roles_{token}";
    }
}
