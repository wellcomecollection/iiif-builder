﻿using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OAuth2;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
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
            services.Configure<BinaryObjectCacheOptionsByType>(Configuration.GetSection("BinaryObjectCache"));
            
            services.AddDlcsClient(Configuration);
            
            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddSingleton<IStatusProvider, S3StatusProvider>(opts =>
                ActivatorUtilities.CreateInstance<S3StatusProvider>(opts,
                    factory.Get(NamedClient.Dds)));
            
            services.AddSingleton<IStorage, S3Storage>(opts =>
                ActivatorUtilities.CreateInstance<S3Storage>(opts, 
                    factory.Get(NamedClient.Dds)));
            
            services.AddMemoryCache();

            services.AddHttpClient<OAuth2ApiConsumer>();

            services
                .AddScoped<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>()
                .AddScoped<StorageServiceClient>()
                .AddScoped<IMetsRepository, MetsRepository>()
                .AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>()
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