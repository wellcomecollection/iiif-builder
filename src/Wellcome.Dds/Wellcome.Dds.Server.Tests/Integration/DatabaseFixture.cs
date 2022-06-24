using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wellcome.Dds.AssetDomainRepositories;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Integration
{
    public class DatabaseFixture : IAsyncLifetime
    {
        protected static string DdsConnectionString { get; } = "host=localhost;port=5430;database=ddsinstrtest;username=ddsinstrumentation_user;password=ddsinstrumentation";
        
        public DdsInstrumentationContext DdsInstrumentationContext { get; private set; }

        private Process process;

        public async Task InitializeAsync()
        {
            if (process != null) return;

            process = Process.Start("docker",
                "run --name dds-server-test -e POSTGRES_USER=ddsinstrumentation_user -e POSTGRES_PASSWORD=ddsinstrumentation -e POSTGRES_DB=ddsinstrtest -p 5430:5432 postgres:alpine");

            var started = await WaitForContainer();
            if (!started)
            {
                throw new Exception($"Startup failed, could not get postgres");
            }

            DdsInstrumentationContext = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseNpgsql(DdsConnectionString)
                    .UseSnakeCaseNamingConvention().Options);
            DdsInstrumentationContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            await DdsInstrumentationContext.Database.MigrateAsync();
        }

        private async Task<bool> WaitForContainer()
        {
            var testTimeout = TimeSpan.FromSeconds(180);
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < testTimeout)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(DdsConnectionString);
                    conn.Open();
                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore exceptions, just retry
                }

                await Task.Delay(1000);
            }

            return false;
        }

        public async Task DisposeAsync()
        {
            await DdsInstrumentationContext.DisposeAsync();
            if (process != null)
            {
                process.Dispose();
                process = null;
            }

            var processStop = Process.Start("docker", "stop dds-server-test");
            await processStop.WaitForExitAsync();
            var processRm = Process.Start("docker", "rm dds-server-test");
            await processRm.WaitForExitAsync();
        }
        
        public void CleanUp()
            => DdsInstrumentationContext.Database.ExecuteSqlRaw("TRUNCATE workflow_jobs");
    }
}