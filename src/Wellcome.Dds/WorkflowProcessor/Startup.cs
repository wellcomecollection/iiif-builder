using Amazon.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.Ingest;
using Wellcome.Dds.AssetDomainRepositories.Mets;

namespace WorkflowProcessor
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

            services.Configure<RunnerOptions>(Configuration.GetSection("WorkflowProcessor"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));
            services.Configure<BinaryObjectCacheOptions>(Configuration.GetSection("BinaryObjectCache:StorageMaps"));
            
            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddSingleton<IStorage, S3Storage>(opts =>
                ActivatorUtilities.CreateInstance<S3Storage>(opts, 
                    factory.Get(NamedClient.Dds)));

            services.AddMemoryCache();
            
            services.AddHttpClient<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>();
            
            services
                .AddScoped<WorkflowRunner>()
                .AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>()
                .AddScoped<IIngestJobRegistry, CloudServicesIngestRegistry>()
                .AddSingleton<IMetsRepository, MetsRepository>()
                .AddHostedService<WorkflowProcessorService>();
            
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