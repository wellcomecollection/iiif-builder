using System;
using System.Net.Http.Headers;
using System.Text;
using DlcsWebClient.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wellcome.Dds.AssetDomain.Dlcs;

namespace DlcsWebClient.Dlcs
{
    public static class ServiceCollectionX
    {
        /// <summary>
        /// Adds <see cref="IDlcs"/> implementation to <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">Current <see cref="IServiceCollection"/> object.</param>
        /// <param name="configuration">Current <see cref="IConfiguration"/> object.</param>
        /// <param name="dlcsSectionName">Configuration name storing dlcs sections.</param>
        /// <returns>IHttpClientBuilder</returns>
        public static IHttpClientBuilder AddDlcsClient(this IServiceCollection services,
            IConfiguration configuration, string dlcsSectionName = "Dlcs")
        {
            var dlcsSection = configuration.GetSection(dlcsSectionName);
            var dlcsOptions = dlcsSection.Get<DlcsOptions>();

            return services.AddHttpClient<IDlcs, Dlcs>(client =>
            {
                var creds = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{dlcsOptions.ApiKey}:{dlcsOptions.ApiSecret}"));
                client.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
                client.Timeout = TimeSpan.FromMilliseconds(360000);
            });
        }
    }
}