using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wellcome.Dds.Identifiers;

namespace Wellcome.Dds.Repositories.Identifiers
{
    public static class IdentifiersApiServiceCollectionExtensions
    {
        public const string SectionName = "IdentifiersApi";

        /// <summary>
        /// Register the Identifiers API client (RFC 089) from the "IdentifiersApi" configuration section,
        /// or, where it is not enabled, a stand-in that answers every lookup with "no mapping" and never
        /// makes a request. The choice is made once at startup from configuration. Also registers the
        /// storage probe and the navigator that compose the registry with the catalogue and storage.
        /// </summary>
        public static IServiceCollection AddIdentifiersApi(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);
            services.Configure<IdentifiersApiOptions>(section);

            var options = new IdentifiersApiOptions();
            section.Bind(options);
            if (options.IsEnabled)
            {
                services.AddHttpClient<IIdentifiersApi, WellcomeCollectionIdentifiersApi>();
            }
            else
            {
                services.AddSingleton<IIdentifiersApi, DisabledIdentifiersApi>();
            }

            // scoped: StorageServiceClient is
            services.AddScoped<IStorageServiceProbe, StorageServiceProbe>();
            services.AddScoped<IIdentifierNavigator, IdentifierNavigator>();
            return services;
        }
    }
}
