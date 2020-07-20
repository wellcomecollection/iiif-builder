using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Wellcome.Dds.Server.Tests.Integration
{
    public class DdsServerAppFactory : WebApplicationFactory<Startup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

            builder
                .ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); });
        }
    }
}