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
using Utils.Aws.S3;
using Utils.Caching;
using Utils.Storage;
using Utils.Storage.FileSystem;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomain.Workflow;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.AssetDomainRepositories.Workflow;
using Wellcome.Dds.Common;

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

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<DdsInstrumentationContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation"))
                .UseSnakeCaseNamingConvention());

            services.AddAuthentication(AzureADDefaults.AuthenticationScheme)
                .AddAzureAD(opts => Configuration.Bind("AzureAd", opts));
            
            var awsOptions = Configuration.GetAWSOptions("Dds-AWS");
            var storageOptions = Configuration.GetAWSOptions("Storage-AWS");

            // How do we have more than one IAmazonS3 - we have two different profiles
            var factory = new NamedAmazonS3ClientFactory();
            factory.Add("Dds", awsOptions.CreateServiceClient<IAmazonS3>());
            factory.Add("Storage", storageOptions.CreateServiceClient<IAmazonS3>());

            services.AddSingleton<INamedAmazonS3ClientFactory>(factory);
            
            services.AddDefaultAWSOptions(awsOptions);

            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage-Production"));

            // we need more than one of these
            services.Configure<BinaryObjectCacheOptions>(Configuration.GetSection("BinaryObjectCache:StorageMaps"));

            // This will require an S3 implementation in production
            // services.AddSingleton<IStorage, FileSystemStorage>();
            services.AddSingleton<IStorage, S3Storage>(opts =>
                ActivatorUtilities.CreateInstance<S3Storage>(opts, 
                    factory.Get("Dds")));

            services.AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>();

            // should cover all the resolved type usages...
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            // Need an HTTPClient to be injected into Dlcs - not WebClient
            services.AddSingleton<IDlcs, Dlcs>();
            // This is the one that needs an IAmazonS3 with the storage profile
            services.AddSingleton<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>(opts =>
                ActivatorUtilities.CreateInstance<ArchiveStorageServiceWorkStorageFactory>(opts,
                    factory.Get("Storage")));
            services.AddSingleton<IMetsRepository, MetsRepository>();

            // TODO - assess the lifecycle of all of these
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<IWorkflowCallRepository, WorkflowCallRepository>();
            services.AddScoped<IDatedIdentifierProvider, RecentlyAddedItemProvider>();
            services.AddControllersWithViews().AddRazorRuntimeCompilation();

            // TODO - add DB health check
            services.AddHealthChecks();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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
            
            app.UseStaticFiles();
            app.UseRouting();
            
            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
                endpoints.MapHealthChecks("/management/healthcheck");
            });
        }
    }
}
