using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Wellcome.Dds.Server.Auth
{
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Add DlcsBasicAuthenticationHandler to service collection
        /// </summary>
        public static AuthenticationBuilder AddBasicAuth(this IServiceCollection services,
            Action<BasicAuthenticationOptions> configureOptions)
            => services.AddAuthentication(BasicAuthenticationDefaults.AuthenticationScheme)
                .AddScheme<BasicAuthenticationOptions, DlcsBasicAuthenticationHandler>(
                    BasicAuthenticationDefaults.AuthenticationScheme, configureOptions);
    }
}