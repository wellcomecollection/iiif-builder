using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Amazon.S3;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OAuth2;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Ingest;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Workflow;
using Wellcome.Dds.Common;
using Wellcome.Dds.Dashboard.Controllers;
using Wellcome.Dds.Repositories;

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
            services.AddDbContext<DdsInstrumentationContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation"))
                .UseSnakeCaseNamingConvention());

            services.AddDbContext<DdsContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("Dds"))
                .UseSnakeCaseNamingConvention());

            services.AddAuthentication(AzureADDefaults.AuthenticationScheme)
                .AddAzureAD(opts => Configuration.Bind("AzureAd", opts));

            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);
            
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions("Dds-AWS"));

            var dlcsSection = Configuration.GetSection("Dlcs");
            var dlcsOptions = dlcsSection.Get<DlcsOptions>();
            
            services.Configure<DlcsOptions>(dlcsSection);
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));

            // we need more than one of these
            services.Configure<BinaryObjectCacheOptions>(Configuration.GetSection("BinaryObjectCache:StorageMaps"));

            // This will require an S3 implementation in production
            //services.AddSingleton<IStorage, FileSystemStorage>();
            services.AddSingleton<IStorage, S3Storage>(opts =>
                ActivatorUtilities.CreateInstance<S3Storage>(opts, 
                    factory.Get(NamedClient.Dds)));

            services.AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>();

            // should cover all the resolved type usages...
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddDlcsClient(Configuration);

            services.AddHttpClient<OAuth2ApiConsumer>();

            // This is the one that needs an IAmazonS3 with the storage profile
            services.AddSingleton<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>();
            services.AddSingleton<IMetsRepository, MetsRepository>();

            services.AddSingleton<IStatusProvider, S3StatusProvider>(opts =>
                ActivatorUtilities.CreateInstance<S3StatusProvider>(opts,
                    factory.Get(NamedClient.Dds)));

            // TODO - assess the lifecycle of all of these
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<IWorkflowCallRepository, WorkflowCallRepository>();
            services.AddScoped<IDatedIdentifierProvider, RecentlyAddedItemProvider>();
            services.AddScoped<IIngestJobRegistry, CloudServicesIngestRegistry>();
            services.AddScoped<IIngestJobProcessor, DashboardCloudServicesJobProcessor>();

            // These are non-working impls atm
            services.AddSingleton<Synchroniser>(); // make this a service provided by IDds
            services.AddSingleton<CacheBuster>(); // Have a think about what this does in the new world - what is it busting?

            services.AddScoped<IDds, Wellcome.Dds.Repositories.Dds>();

            services.AddControllersWithViews(
                opts => opts.Filters.Add(typeof(DashGlobalsActionFilter)))
                .AddRazorRuntimeCompilation();

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
            
            if (env.IsDevelopment())
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
            
            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
                endpoints.MapControllerRoute("Default", "{controller}/{action}/{id?}/{*parts}",
                    defaults: new { controller = "Dash", action = "Index" }
                );
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
