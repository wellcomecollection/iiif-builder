using Microsoft.AspNetCore.Authentication;

namespace Wellcome.Dds.Server.Auth
{
    /// <summary>
    /// Options for user with <see cref="DlcsBasicAuthenticationHandler"/>
    /// </summary>
    public class BasicAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        /// Get or set the Realm for use in auth challenges.
        /// </summary>
        public string Realm { get; set; }

    }
}