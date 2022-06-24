using System;
using System.Linq;
using Community.Microsoft.Extensions.Caching.PostgreSql;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using OAuth2;
using Utils.Aws.Options;
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Storage.WellcomeStorageService;
using Wellcome.Dds.Auth.Web;
using Wellcome.Dds.Auth.Web.Sierra;
using Wellcome.Dds.Catalogue;
using Wellcome.Dds.Common;
using Wellcome.Dds.IIIFBuilding;
using Wellcome.Dds.Repositories;
using Wellcome.Dds.Repositories.Catalogue;
using Wellcome.Dds.Repositories.Presentation;
using Wellcome.Dds.Repositories.WordsAndPictures;
using Wellcome.Dds.Server.Auth;
using Wellcome.Dds.Server.Conneg;
using Wellcome.Dds.Server.Controllers;
using Wellcome.Dds.Server.Infrastructure;
using Wellcome.Dds.WordsAndPictures;

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
            // Use pre-v6 handling of datetimes for npgsql
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            
            // Temporarily here to demonstrate IIIFPrecursor - should not be required in production DDS.Server
            services.AddDbContext<DdsInstrumentationContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation"))
                .UseSnakeCaseNamingConvention());

            var ddsConnectionString = Configuration.GetConnectionString("Dds");
            services.AddDbContext<DdsContext>(options => options
                .UseNpgsql(ddsConnectionString)
                .UseSnakeCaseNamingConvention());

            services.AddMemoryCache();

            if (!WebHostEnvironment.IsEnvironment("Testing"))
            {
                services.AddDistributedPostgreSqlCache(setup =>
                {
                    setup.ConnectionString = ddsConnectionString;
                    setup.SchemaName = "public";
                    setup.TableName = "__dist_cache";
                    setup.CreateInfrastructure = !WebHostEnvironment.IsProduction();
                    setup.DefaultSlidingExpiration = TimeSpan.FromMinutes(20); // TODO - is this right?
                });
            }

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromSeconds(3600);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddSwagger();
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()); // we might need to customise CORS handling for IIIF Auth...
            });
            services.AddMvc(options =>
            {
                var jsonFormatter = options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().FirstOrDefault();
                jsonFormatter?.SupportedMediaTypes.Add(IIIFPresentation.ContentTypes.V2);
                jsonFormatter?.SupportedMediaTypes.Add(IIIFPresentation.ContentTypes.V3);
            });

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
            services.Configure<SierraRestApiOptions>(Configuration.GetSection("SierraRestAPI"));
            services.Configure<S3CacheOptions>(Configuration.GetSection("S3CacheOptions"));

            // we need more than one of these
            services.Configure<BinaryObjectCacheOptionsByType>(Configuration.GetSection("BinaryObjectCache"));
            
            services.AddBasicAuth(opts => opts.Realm = "Wellcome");

            // This will require an S3 implementation in production
            //services.AddSingleton<IStorage, FileSystemStorage>();
            services.AddSingleton<IStorage, S3CacheAwareStorage>(opts =>
                ActivatorUtilities.CreateInstance<S3CacheAwareStorage>(opts, 
                    factory.Get(NamedClient.Dds)));

            services.AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>();
            services.AddHttpClient<ICatalogue, WellcomeCollectionCatalogue>();
            
            services.AddSingleton<UriPatterns>();
            services.AddSingleton<Helpers>();

            // should cover all the resolved type usages...
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            services.AddDlcsClient(Configuration);

            // This is the one that needs an IAmazonS3 with the storage profile
            services.AddHttpClient<OAuth2ApiConsumer>();
            services.AddScoped<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>()
                .AddScoped<Synchroniser>()
                .AddScoped<IDds, Repositories.Dds>()
                .AddScoped<StorageServiceClient>()
                .AddScoped<IIIIFBuilder, IIIFBuilder>()
                .AddScoped<IMetsRepository, MetsRepository>()
                .AddScoped<IDashboardRepository, DashboardRepository>()
                .AddScoped<ISearchTextProvider, CachingAltoSearchTextProvider>()
                .AddScoped<CachingAltoSearchTextProvider>()
                .AddScoped<AltoSearchTextProvider>()
                .AddSingleton<PdfThumbnailUtil>();

            services.AddSingleton<IAuthenticationService, SierraRestPatronApi>();
            // services.AddSingleton<IAuthenticationService, AllowAllAuthenticator>();
            services.AddSingleton<IUserService, SierraRestPatronApi>();
            services.AddSingleton<MillenniumIntegration>();

            services.AddControllers().AddJsonOptions(
                options => {
                    options.JsonSerializerOptions.IgnoreNullValues = true;
                    options.JsonSerializerOptions.WriteIndented = true;
                });

            services.AddFeatureManagement();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("CorsPolicy");
            app.SetupSwagger();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();
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