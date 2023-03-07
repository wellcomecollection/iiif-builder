using System;
using Amazon.SimpleNotificationService;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web.UI;
using OAuth2;
using Utils.Aws.Options;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
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
using Wellcome.Dds.Dashboard.Controllers;
using Wellcome.Dds.Dashboard.Models;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Catalogue;
using Wellcome.Dds.Repositories.Presentation;

namespace Wellcome.Dds.Dashboard
{
    public class Startup
    {
        private IConfiguration Configuration { get; }
        private IWebHostEnvironment WebHostEnvironment { get; }

        public Startup(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            Configuration = configuration;
            WebHostEnvironment = webHostEnvironment;
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

            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(Configuration);

            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);
            
            var ddsAwsOptions = Configuration.GetAWSOptions("Dds-AWS");
            var platformAwsOptions = Configuration.GetAWSOptions("Platform-AWS");
            services.AddDefaultAWSOptions(ddsAwsOptions);   
            services.AddAWSService<IAmazonSimpleNotificationService>(platformAwsOptions);

            var dlcsSection = Configuration.GetSection("Dlcs");
            var dlcsOptions = dlcsSection.Get<DlcsOptions>();
            
            services.Configure<DlcsOptions>(dlcsSection);
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));
            services.Configure<DashOptions>(Configuration.GetSection("Dash"));
            services.Configure<S3CacheOptions>(Configuration.GetSection("S3CacheOptions"));
            services.Configure<CacheInvalidationOptions>(Configuration.GetSection("CacheInvalidation"));

            // we need more than one of these
            services.Configure<BinaryObjectCacheOptionsByType>(Configuration.GetSection("BinaryObjectCache"));

            // This will require an S3 implementation in production
            //services.AddSingleton<IStorage, FileSystemStorage>();
            services.AddSingleton<IStorage, S3CacheAwareStorage>(opts =>
                ActivatorUtilities.CreateInstance<S3CacheAwareStorage>(opts, 
                    factory.Get(NamedClient.Dds)));

            services.AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>();
            services.AddSingleton<UriPatterns>();
            services.AddSingleton<LinkRewriter>();

            // should cover all the resolved type usages...
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddDlcsClient(Configuration);

            services.AddHttpClient<OAuth2ApiConsumer>();
            
            // We need this even though dashboard doesn't use it, because this is the startup for Database Migrations
            // DdsContext is in Wellcome.Dds.Repositories
            services.AddHttpClient<ICatalogue, WellcomeCollectionCatalogue>();

            // This is the one that needs an IAmazonS3 with the storage profile
            services.AddScoped<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>()
                .AddScoped<StorageServiceClient>()
                .AddScoped<IMetsRepository, MetsRepository>();

            services.AddScoped<IStatusProvider, DatabaseStatusProvider>();

            // TODO - assess the lifecycle of all of these
            services.AddScoped<IDigitalObjectRepository, DigitalObjectRepository>();
            services.AddScoped<IWorkflowCallRepository, WorkflowCallRepository>();
            services.AddScoped<IDatedIdentifierProvider, RecentlyAddedItemProvider>();
            services.AddScoped<IIngestJobRegistry, CloudServicesIngestRegistry>();
            services.AddScoped<IIngestJobProcessor, DashboardCloudServicesJobProcessor>();
            services.AddScoped<IIIIFBuilder, IIIFBuilder>();
            services.AddScoped<ManifestationModelBuilder>(opts =>
                ActivatorUtilities.CreateInstance<ManifestationModelBuilder>(opts, factory.Get(NamedClient.Dds)));

            services.AddSingleton<ICacheInvalidationPathPublisher, CacheInvalidationPathPublisher>();
            
            
            // These are non-working impls atm
            services.AddScoped<Synchroniser>(); // make this a service provided by IDds

            services.AddScoped<IDds, Wellcome.Dds.Repositories.Dds>();

            services.AddControllersWithViews(
                opts => opts.Filters.Add(typeof(DashGlobalsActionFilter)))
                .AddRazorRuntimeCompilation()
                .AddMicrosoftIdentityUI();

            services.AddHealthChecks()
                .AddDbContextCheck<DdsInstrumentationContext>("DdsInstrumentation-db")
                .AddDbContextCheck<DdsContext>("Dds-db")
                .AddUrlGroup(new Uri(dlcsOptions.ApiEntryPoint), "DLCS API");
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (!env.IsProduction())
            {
                UpdateDatabase(logger);
            }
            
            if (env.IsDevelopment() || env.EnvironmentName == "Staging-New")
            {
                app.UseDeveloperExceptionPage();
            }

            // This is required for ADAuth on linux containers. When hosting in ECS we are doing ssl termination
            // at load-balancer, so by default redirect will be http - this ensures https
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedProto
            });

            app.UsePathBase("/dash");
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints
                    .MapControllers()
                    .RequireAuthorization();

                string actionRegex = "^(?!account|workflowcall|job|log|peek|settings).*$";
                endpoints.MapControllerRoute(name: "dash",
                    pattern: "{action}/{id?}/{*parts}",
                    constraints: new { action = actionRegex, },
                    defaults: new { controller = "Dash" });
                endpoints.MapControllerRoute("Default", "{controller=Dash}/{action=Index}/{id?}/{*parts}");
                endpoints.MapHealthChecks("/management/healthcheck");
            });
        }

        private void UpdateDatabase(ILogger<Startup> logger)
        {
            var ddsInstrumentationConnection = Configuration.GetConnectionString("DdsInstrumentation");
            using (var context = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseNpgsql(ddsInstrumentationConnection)
                    .UseSnakeCaseNamingConvention()
                    .Options))
            {
                logger.LogInformation("Running migrations on DdsInstrumentation");
                context.Database.Migrate();
            }

            var ddsConnection = Configuration.GetConnectionString("Dds");
            using (var context = new DdsContext(
                new DbContextOptionsBuilder<DdsContext>()
                    .UseNpgsql(ddsConnection)
                    .UseSnakeCaseNamingConvention()
                    .Options))
            {
                logger.LogInformation("Running migrations on Dds");
                context.Database.Migrate();
            }
        }
    }
}
