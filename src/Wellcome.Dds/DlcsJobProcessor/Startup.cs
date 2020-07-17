using DlcsWebClient.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Utils.Aws.S3;
using Utils.Caching;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Ingest;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Common;

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
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation"))
                .UseSnakeCaseNamingConvention());
            
            services.Configure<JobProcessorOptions>(Configuration.GetSection("JobProcessor"));
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));
            services.Configure<BinaryObjectCacheOptions>(Configuration.GetSection("BinaryObjectCache:StorageMaps"));
            
            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddSingleton<IStatusProvider, S3StatusProvider>(opts =>
                ActivatorUtilities.CreateInstance<S3StatusProvider>(opts,
                    factory.Get(NamedClient.Dds)));
            
            services.AddMemoryCache();
            
            services.AddHttpClient<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>();

            services
                .AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>()
                .AddSingleton<IMetsRepository, MetsRepository>()
                .AddScoped<IDashboardRepository, DashboardRepository>()
                .AddScoped<IIngestJobProcessor, DashboardCloudServicesJobProcessor>()
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