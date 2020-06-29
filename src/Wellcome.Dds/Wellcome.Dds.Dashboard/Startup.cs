using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DlcsWebClient.Config;
using DlcsWebClient.Dlcs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomainRepositories.Dashboard;
using Wellcome.Dds.AssetDomainRepositories.WorkflowJobs;

namespace Wellcome.Dds.Dashboard
{
    public class Startup
    {
        private IConfiguration Configuration { get; set; }

        public Startup(IConfiguration config)
        {
            Configuration = config;
        }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<WorkflowContext>(); // 

            // Need an HTTPClient to be injected into Dlcs - not WebClient
            services.AddSingleton<IDlcs, Dlcs>();
            services.AddSingleton<IDashboardRepository, DashboardRepository>();
            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));
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
