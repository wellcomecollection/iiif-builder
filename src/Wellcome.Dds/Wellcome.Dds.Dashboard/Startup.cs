using Amazon.S3;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Utils.Caching;
using Utils.Storage;
using Utils.Storage.FileSystem;
using Utils.Storage.S3;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Mets;
using Wellcome.Dds.AssetDomainRepositories;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.Mets;
using Wellcome.Dds.Common;

namespace Wellcome.Dds.Dashboard
{
    public class Startup
    {
        private IConfiguration Configuration { get; set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<DdsInstrumentationContext>(options => options
                .UseNpgsql(Configuration.GetConnectionString("DdsInstrumentation"))
                .UseSnakeCaseNamingConvention());

            var ddsAwsOptions = Configuration.GetAWSOptions("Dds-AWS");
            var storageAwsOptions = Configuration.GetAWSOptions("Storage-AWS");
            // How do we have more than one IAmazonS3 - we have two different profiles

            // method 1 - doesn't work because you need IAmazonS3 here:
            // https://github.com/aws/aws-sdk-net/blob/master/extensions/src/AWSSDK.Extensions.NETCore.Setup/ClientFactory.cs#L171
            // services.AddAWSService<IAmazonS3ForCacheStorage>(ddsAwsOptions);
            // services.AddAWSService<IAmazonS3ForWellcomeStorageService>(storageAwsOptions);

            // method 2, and inject IEnumerable<IAmazonS3>
            // it sets it up OK, but there's noting to distinguish the 
            services.AddAWSService<IAmazonS3>(ddsAwsOptions);
            services.AddAWSService<IAmazonS3>(storageAwsOptions);

            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));
            services.Configure<DdsOptions>(Configuration.GetSection("Dds"));
            services.Configure<StorageOptions>(Configuration.GetSection("Storage-Production"));

            // we need more than one of these
            services.Configure<BinaryObjectCacheOptions>(Configuration.GetSection("BinaryObjectCache:StorageMaps"));

            // This will require an S3 implementation in production
            services.AddSingleton<ICacheStorage, FileSystemCacheStorage>();

            services.AddSingleton<ISimpleCache, ConcurrentSimpleMemoryCache>();

            // should cover all the resolved type usages...
            services.AddSingleton(typeof(IBinaryObjectCache<>), typeof(BinaryObjectCache<>));

            // Need an HTTPClient to be injected into Dlcs - not WebClient
            services.AddSingleton<IDlcs, Dlcs>();
            // This is the one that needss an IAmazonS3 with the storage profile
            services.AddSingleton<IWorkStorageFactory, ArchiveStorageServiceWorkStorageFactory>();
            services.AddSingleton<IMetsRepository, MetsRepository>();
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
