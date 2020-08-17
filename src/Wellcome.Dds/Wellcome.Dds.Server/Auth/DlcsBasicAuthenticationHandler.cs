using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Server.Auth
{
    /// <summary>
    /// <see cref="AuthenticationHandler{TOptions}"/> implementation for verifying DLCS auth.
    /// </summary>
    /// <remarks>The only thing tying this to DLCS creds is IsValidUser(), can be changed if need be.</remarks>
    public class DlcsBasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
    {
        private readonly DdsOptions ddsOptions;
        private const string AuthHeader = "Authorization";
        private const string BasicScheme = "Basic";
        
        public DlcsBasicAuthenticationHandler(
            IOptionsMonitor<BasicAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IOptions<DdsOptions> ddsOptions)
            : base(options, logger, encoder, clock)
        {
            this.ddsOptions = ddsOptions.Value;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(AuthHeader))
            {
                //Authorization header not in request
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!AuthenticationHeaderValue.TryParse(Request.Headers[AuthHeader],
                out AuthenticationHeaderValue headerValue))
            {
                //Invalid Authorization header
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!BasicScheme.Equals(headerValue.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                //Not Basic authentication header
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var userAndPassword = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue.Parameter));
            string[] cred = userAndPassword.Split(':');
            if (cred.Length != 2)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid Basic authentication header"));
            }

            var user = new {Name = cred[0], Pass = cred[1]};
            if (!IsValidUser(user.Name, user.Pass))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));
            }

            var claims = new[] {new Claim(ClaimTypes.Name, user.Name)};
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Options.Realm}\"";
            return base.HandleChallengeAsync(properties);
        }

        private bool IsValidUser(string name, string pass)
            => name == ddsOptions.DlcsOriginUsername && pass == ddsOptions.DlcsOriginPassword;
    }
}