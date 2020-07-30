using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Utils;
using Utils.Web;
using Wellcome.Dds.Auth.Web;
using Wellcome.Dds.Common;
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
        private MillenniumIntegration millenniumIntegration;
        private IDistributedCache distributedCache;
        private DdsOptions ddsOptions;

        public RoleProviderController(
            MillenniumIntegration millenniumIntegration,
            IDistributedCache distributedCache,
            IOptions<DdsOptions> options
        )
        {
            this.millenniumIntegration = millenniumIntegration;
            this.distributedCache = distributedCache;
            this.ddsOptions = options.Value;
        }

        private const string dlcsReturnUrl = "https://dlcs.io/auth/2/fromcas";
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
        public async Task<ActionResult> DlcsLoginAsync()
        {
            var session = HttpContext.Session;
            const string sessionFlagKey = "roles_acquired";
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.AppendStandardNoCacheHeaders();
            var username = Request.Form["directlogin_u"].FirstOrDefault();
            var password = Request.Form["directlogin_p"].FirstOrDefault();
            LoginResult loginResult = null;
            if (username.HasText())
            {               
                loginResult = await millenniumIntegration.LoginWithMillenniumAsync(username, password);
                if (loginResult.Success)
                {
                    session.SetInt32(sessionFlagKey, 1);
                    distributedCache.SetString(GetRolesKey(session), loginResult.Roles.ToString());
                    return new RedirectResult(dlcsReturnUrl + "?token=" + session.Id);
                }
            }
            var sessionFlag = session.GetInt32(sessionFlagKey) ?? 0;
            if (sessionFlag == 1)
            {
                // The user has logged in; did we store their roles?
                var storedRoleString = distributedCache.GetString(GetRolesKey(session));
                if (storedRoleString.HasText())
                {
                    return new RedirectResult(dlcsReturnUrl + "?token=" + session.Id);
                }
                // We recognised the session, but don't have roles stored that the DLCS could retrieve
                session.SetInt32(sessionFlagKey, 0);
            }
            var model = new LoginModel
            {
                Username = username,
                Message = loginResult.Message
            };
            return View(model);
        }

        private string GetRolesKey(ISession session)
        {
            return GetRolesKey(session.Id);
        }

        private string GetRolesKey(string token)
        {
            return $"roles_{token}";
        }

        /// <summary>
        /// This is what the DLCS calls out of band using its secret knowledge
        /// </summary>
        /// <returns></returns>
        public ActionResult RolesForToken()
        {
            string token = Request.Query["token"].FirstOrDefault();
            if (AuthorizeDlcs(Request, Response) && token.HasText())
            {
                const string clickthrough = "https://api.dlcs.io/customers/2/roles/clickthrough";
                const string clinicalImages = "https://api.dlcs.io/customers/2/roles/clinicalImages";
                const string restrictedFiles = "https://api.dlcs.io/customers/2/roles/restrictedFiles";
                // const string closed = "https://api.dlcs.io/customers/2/roles/closed";

                var storedRoles = distributedCache.GetString(GetRolesKey(token));
                if(storedRoles.HasText())
                {
                    var sierraRoles = new Roles(storedRoles);
                    var roles = new HashSet<string>();
                    if (sierraRoles.HasAcceptedTerms)
                    {
                        roles.Add(clickthrough);
                    }
                    if (sierraRoles.IsHealthCareProfessional)
                    {
                        roles.Add(clickthrough);
                        roles.Add(clinicalImages);
                    }
                    if (sierraRoles.IsWellcomeStaffMember)
                    {
                        roles.Add(clickthrough);
                        roles.Add(clinicalImages);
                        roles.Add(restrictedFiles);
                    }
                    return Json(roles.ToArray());
                }
                return NotFound("Could not find user for token " + token);
            }
            return null;
        }

        private bool AuthorizeDlcs(HttpRequest request, HttpResponse response)
        {
            var authz = request.Headers["Authorization"].FirstOrDefault();
            if (!authz.HasText())
            {
                response.StatusCode = 401;
                response.Headers.Add("WWW-Authenticate", "Basic realm=\"Wellcome\"");
                return false;
            }
            var cred = Encoding.ASCII.GetString(Convert.FromBase64String(authz.Substring(6))).Split(':');
            var user = new { Name = cred[0], Pass = cred[1] };
            if (user.Name == ddsOptions.DlcsOriginUsername && user.Pass == ddsOptions.DlcsOriginPassword)
            {
                return true;
            }
            response.StatusCode = 403;
            return false;
        }
    }


}
