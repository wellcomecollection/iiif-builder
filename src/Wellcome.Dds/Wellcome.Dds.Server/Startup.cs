using System;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OAuth2;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Ingest;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Ingest;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Workflow;
using Wellcome.Dds.Auth.Web;
using Wellcome.Dds.Auth.Web.Sierra;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Catalogue;
using Wellcome.Dds.Server.Infrastructure;

namespace Wellcome.Dds.Server
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
            // Temporarily here to demonstrate IIIFPrecursor - should not be required in production DDS.Server
            services.AddDbContext<DdsInstrumentationContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation"))
                .UseSnakeCaseNamingConvention());
            
            services.AddDbContext<DdsContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("Dds"))
                .UseSnakeCaseNamingConvention());

            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromSeconds(3600);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddSwagger();
            services.AddCors();
            services.AddMvc();

            services.AddHealthChecks()
                .AddDbContextCheck<DdsContext>("Dds-db");

            // The following setup pulls in everything required to read from SOURCES
            // The new DDS should not need all of this, because it will do most of its work
            // proxying things from S3 that have been built by the other processors.
            // This is a temporary setup to demonstrate the RAW materials for IIIF building.
            
            var factory = services.AddNamedS3Clients(Configuration, NamedClient.All);
            
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions("Dds-AWS"));
            
            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));
            services.Configure<SierraRestAPIOptions>(Configuration.GetSection("SierraRestAPI"));

            // we need more than one of these
            services.Configure<BinaryObjectCacheOptions>(Configuration.GetSection("BinaryObjectCache:StorageMaps"));

            // This will require an S3 implementation in production
            //services.AddSingleton<IStorage, FileSystemStorage>();
            services.AddSingleton<IStorage, S3Storage>(opts =>
                ActivatorUtilities.CreateInstance<S3Storage>(opts, 
                    factory.Get(NamedClient.Dds)));

            services.AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>();
            services.AddHttpClient<ICatalogue, WellcomeCollectionCatalogue>();

            // should cover all the resolved type usages...
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddDlcsClient(Configuration);

            // This is the one that needs an IAmazonS3 with the storage profile
            services.AddHttpClient<OAuth2ApiConsumer>();
            services.AddSingleton<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>();
            services.AddSingleton<IMetsRepository, MetsRepository>();
            services.AddScoped<IDashboardRepository, DashboardRepository>();

            services.AddSingleton<IAuthenticationService, SierraRestPatronAPI>();
            // services.AddSingleton<IAuthenticationService, AllowAllAuthenticator>();
            services.AddSingleton<IUserService, SierraRestPatronAPI>();
            services.AddSingleton<MillenniumIntegration>();

            services.AddControllers().AddJsonOptions(
                options => {
                    options.JsonSerializerOptions.IgnoreNullValues = true;
                    options.JsonSerializerOptions.WriteIndented = true;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();
            app.SetupSwagger();
            app.UseStaticFiles();
            app.UseRouting();
            // For discussion: we only need session state on one particular controller.
            // app.UseSession();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
                endpoints.MapHealthChecks("/management/healthcheck");
            });
        }
    }
}