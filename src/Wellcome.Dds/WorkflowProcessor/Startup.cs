using System;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
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
using Wellcome.Dds;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.DigitalObjects;
using Wellcome.Dds.AssetDomainRepositories.Ingest;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Wellcome.Dds.AssetDomainRepositories.Workflow;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Catalogue;
using Wellcome.Dds.Repositories.Presentation;
using Wellcome.Dds.Repositories.WordsAndPictures;
using Wellcome.Dds.WordsAndPictures;

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
            // Use pre-v6 handling of datetimes for npgsql
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            
            services.AddDbContext<DdsInstrumentationContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation"))
                .UseSnakeCaseNamingConvention());
            
            services.AddDbContext<DdsContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("Dds"))
                .UseSnakeCaseNamingConvention());

            services.Configure<CacheInvalidationOptions>(Configuration.GetSection("CacheInvalidation"));
            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<RunnerOptions>(Configuration.GetSection("WorkflowProcessor"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));
            services.Configure<BinaryObjectCacheOptionsByType>(Configuration.GetSection("BinaryObjectCache"));
            services.Configure<S3CacheOptions>(Configuration.GetSection("S3CacheOptions"));

            var ddsAwsOptions = Configuration.GetAWSOptions("Dds-AWS");
            var platformAwsOptions = Configuration.GetAWSOptions("Platform-AWS");
            services.AddDefaultAWSOptions(ddsAwsOptions);   
            services.AddAWSService<IAmazonSimpleNotificationService>(platformAwsOptions);
            services.AddAWSService<IAmazonSQS>(ddsAwsOptions); // the right one?
            
            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);

            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));
            services.AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>();
            services.AddSingleton<UriPatterns>();

            services.AddSingleton<IStorage, S3CacheAwareStorage>(opts =>
                ActivatorUtilities.CreateInstance<S3CacheAwareStorage>(opts, 
                    factory.Get(NamedClient.Dds)));
            services.AddScoped<IIIIFBuilder, IIIFBuilder>();
            services.AddScoped<BucketWriter>(opts =>
                ActivatorUtilities.CreateInstance<BucketWriter>(opts,
                    factory.Get(NamedClient.Dds)));

            services.AddMemoryCache();
            
            services.AddHttpClient<OAuth2ApiConsumer>();
            services.AddDlcsClient(Configuration);
            services.AddHttpClient<ICatalogue, WellcomeCollectionCatalogue>();
            services
                .AddScoped<IMetsRepository, MetsRepository>()
                .AddScoped<IIngestJobRegistry, CloudServicesIngestRegistry>()
                .AddScoped<IDigitalObjectRepository, DigitalObjectRepository>()
                .AddScoped<IWorkflowCallRepository, WorkflowCallRepository>()
                .AddScoped<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>()
                .AddScoped<StorageServiceClient>()
                .AddScoped<Synchroniser>()
                .AddScoped<IDds, Dds>()
                .AddScoped<AltoSearchTextProvider>()
                .AddScoped<ISearchTextProvider, AltoSearchTextProvider>()
                .AddScoped<CachingAltoSearchTextProvider>()
                .AddScoped<CachingAllAnnotationProvider>()
                .AddScoped<AltoDerivedAssetBuilder>()
                .AddScoped<WorkflowRunner>()
                .AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>()
                .AddSingleton<ICacheInvalidationPathPublisher, CacheInvalidationPathPublisher>()
                .AddScoped<IStatusProvider, DatabaseStatusProvider>()
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