using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wellcome.Dds.Server.Tests.Integration
{
    public class DdsServerAppFactory : WebApplicationFactory<Startup>
    {
        private readonly Dictionary<string, string> configuration = new();

        /// <summary>
        /// Specify connection string to use for dds instrumentation context when building services
        /// </summary>
        /// <param name="connectionString">connection string to use for instrumentation dbContext</param>
        /// <returns>Current instance</returns>
        public DdsServerAppFactory WithInstrumentationConnectionString(string connectionString)
        {
            configuration["ConnectionStrings:DdsInstrumentation"] = connectionString;
            return this;
        }
        
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

            builder
                .ConfigureTestServices(collection => collection.AddDistributedMemoryCache())
                .ConfigureAppConfiguration((context, conf) =>
                {
                    conf.AddJsonFile(configPath);
                    conf.AddInMemoryCollection(configuration);
                })
                .UseEnvironment("Testing");
        }
    }
}