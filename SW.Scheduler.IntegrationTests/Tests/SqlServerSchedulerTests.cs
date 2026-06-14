using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SW.Scheduler.EfCore;
using SW.Scheduler.IntegrationTests.DbContexts;
using SW.Scheduler.IntegrationTests.Fixtures;
using SW.Scheduler.IntegrationTests.Infrastructure;
using SW.Scheduler.SqlServer;
using Xunit;

namespace SW.Scheduler.IntegrationTests.Tests;

/// <summary>
/// Runs the full test suite against a SQL Server container.
/// Per-test isolation via SQL Server schemas (e.g. "sch_a3f92b1c").
/// </summary>
[Collection("SchedulerIntegration")]
public class SqlServerSchedulerTests : SchedulerTestBase, IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public SqlServerSchedulerTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    protected override async Task<HostHandle> CreateHostAsync()
    {
        var schema = $"sch_{Guid.NewGuid():N}"[..16];

        await _fixture.CreateSchemaAsync(schema);

        var schemaOptions = new SchemaOptions(schema);
        var connStr       = _fixture.ConnectionString;

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(schemaOptions);

                services.AddDbContext<SqlServerTestDbContext>(opt =>
                    opt.UseSqlServer(connStr)
                       .ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>());

                services.AddSqlServerScheduler(
                    connectionString: connStr,
                    configure:        o => o.Schema = schema,
                    assemblies:       typeof(SqlServerSchedulerTests).Assembly);

                services.AddSchedulerMonitoring<SqlServerTestDbContext>();
            })
            .Build();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SqlServerTestDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await host.StartAsync();
        await Task.Delay(400);

        return new HostHandle(host, cleanup: async () =>
            await _fixture.DropSchemaAsync(schema));
    }
}
