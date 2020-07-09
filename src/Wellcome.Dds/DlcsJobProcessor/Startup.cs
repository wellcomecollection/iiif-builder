using DlcsWebClient.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddHealthChecks();
            
            services.Configure<DlcsOptions>(Configuration.GetSection("Dlcs"));

            services.AddHostedService<DashboardContinuousRunningStrategy>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapHealthChecks("/management/healthcheck"));
        }
    }
}