using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wellcome.Dds.Server.Tests.Integration
{
    public class DdsServerAppFactory : WebApplicationFactory<Startup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

            builder
                .ConfigureTestServices(collection => collection.AddDistributedMemoryCache())
                .ConfigureAppConfiguration((context, conf) => { conf.AddJsonFile(configPath); })
                .UseEnvironment("Testing");
        }
    }
}