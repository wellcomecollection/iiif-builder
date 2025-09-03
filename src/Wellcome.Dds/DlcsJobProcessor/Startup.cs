using System;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OAuth2;
using Utils.Aws.Options;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.DigitalObjects;
using Wellcome.Dds.AssetDomainRepositories.Ingest;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories.Catalogue;

namespace DlcsJobProcessor
{
    public class Startup
    {
        private IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<DdsInstrumentationContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation")!)
                .UseSnakeCaseNamingConvention());
            
            services.Configure<JobProcessorOptions>(Configuration.GetSection("JobProcessor"));
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));
            services.Configure<BinaryObjectCacheOptionsByType>(Configuration.GetSection("BinaryObjectCache"));
            services.Configure<S3CacheOptions>(Configuration.GetSection("S3CacheOptions"));

            services.AddDlcsClient(Configuration);
            
            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddScoped<IStatusProvider, DatabaseStatusProvider>();
            
            services.AddSingleton<IStorage, S3CacheAwareStorage>(opts =>
                ActivatorUtilities.CreateInstance<S3CacheAwareStorage>(opts, 
                    factory.Get(NamedClient.Dds)));
            
            services.AddMemoryCache();

            services.AddHttpClient<OAuth2ApiConsumer>();
            services.AddHttpClient<ICatalogue, WellcomeCollectionCatalogue>();

            services
                .AddScoped<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>()
                .AddScoped<StorageServiceClient>()
                .AddScoped<IMetsRepository, MetsRepository>()
                .AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>()
                .AddScoped<IDigitalObjectRepository, DigitalObjectRepository>()
                .AddScoped<IIngestJobProcessor, DashboardCloudServicesJobProcessor>()
                .AddSingleton<UriPatterns>()
                .AddHostedService<DashboardContinuousRunningStrategy>();
            
            services.AddHealthChecks()
                .AddDbContextCheck<DdsInstrumentationContext>("DdsInstrumentation-db");
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapHealthChecks("/management/healthcheck"));
        }
    }
}