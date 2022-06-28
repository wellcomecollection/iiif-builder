using System.Threading.Tasks;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Configurations.Databases;
using DotNet.Testcontainers.Containers.Modules.Databases;
using Microsoft.EntityFrameworkCore;
using Wellcome.Dds.AssetDomainRepositories;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Integration
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly PostgreSqlTestcontainer postgresContainer;
        
        public string DdsInstrumentationConnectionString { get; }
        
        public DdsInstrumentationContext DdsInstrumentationContext { get;  }

        public DatabaseFixture()
        {
            var postgresBuilder = new TestcontainersBuilder<PostgreSqlTestcontainer>()
                .WithDatabase(new PostgreSqlTestcontainerConfiguration("postgres:12-alpine")
                {
                    Database = "db",
                    Password = "postgres_pword",
                    Username = "postgres"
                })
                .WithCleanUp(true)
                .WithLabel("wellcomedds_test", "True");
            
            postgresContainer = postgresBuilder.Build();
            DdsInstrumentationConnectionString = postgresContainer.ConnectionString;
            
            DdsInstrumentationContext = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseNpgsql(DdsInstrumentationConnectionString)
                    .UseSnakeCaseNamingConvention().Options);
            DdsInstrumentationContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public async Task InitializeAsync()
        {
            await postgresContainer.StartAsync();
            await DdsInstrumentationContext.Database.MigrateAsync();
        }

        public Task DisposeAsync() => postgresContainer.StopAsync();

        public void CleanUp()
            => DdsInstrumentationContext.Database.ExecuteSqlRaw("TRUNCATE workflow_jobs");
    }
}