using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Wellcome.Dds.AssetDomainRepositories;
using Xunit;

namespace Wellcome.Dds.Server.Tests.Integration
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("db")
            .WithUsername("postgres")
            .WithPassword("postgres_pword")
            .WithCleanUp(true)
            .WithLabel("wellcomedds_test", "True")
            .Build();

        public string DdsInstrumentationConnectionString { get; private set; } = "";

        public DdsInstrumentationContext DdsInstrumentationContext { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await postgresContainer.StartAsync();
            DdsInstrumentationConnectionString = postgresContainer.GetConnectionString();
            DdsInstrumentationContext = new DdsInstrumentationContext(
                new DbContextOptionsBuilder<DdsInstrumentationContext>()
                    .UseNpgsql(DdsInstrumentationConnectionString)
                    .UseSnakeCaseNamingConvention().Options,
                new NullLogger<DdsInstrumentationContext>());
            DdsInstrumentationContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            await DdsInstrumentationContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await DdsInstrumentationContext.DisposeAsync();
            await postgresContainer.DisposeAsync();
        }

        public void CleanUp()
            => DdsInstrumentationContext.Database.ExecuteSqlRaw("TRUNCATE workflow_jobs");
    }
}
