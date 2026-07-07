using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SW.Scheduler.EfCore;
using SW.Scheduler.IntegrationTests.DbContexts;
using SW.Scheduler.IntegrationTests.Fixtures;
using SW.Scheduler.IntegrationTests.Infrastructure;
using SW.Scheduler.PgSql;
using Xunit;

namespace SW.Scheduler.IntegrationTests.Tests;

/// <summary>
/// Runs the full test suite against a PostgreSQL container.
/// The container starts once for the test class; each test method gets a fresh
/// schema so Quartz tables are fully isolated.
/// </summary>
[Collection("SchedulerIntegration")]
public class PostgreSqlSchedulerTests : SchedulerTestBase, IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlSchedulerTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    protected override async Task<HostHandle> CreateHostAsync()
    {
        // Each test gets its own schema: e.g. "sch_a3f92b1c"
        var schema = $"sch_{Guid.NewGuid():N}"[..16];

        await _fixture.CreateSchemaAsync(schema);

        var schemaOptions = new SchemaOptions(schema);
        var connStr       = _fixture.ConnectionString;

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(schemaOptions);

                services.AddDbContext<PgSqlTestDbContext>(opt =>
                    opt.UseNpgsql(connStr)
                       .ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>());

                services.AddPgSqlScheduler(
                    connectionString: connStr,
                    schema:           schema,
                    assemblies:       typeof(PostgreSqlSchedulerTests).Assembly);

                services.AddSchedulerMonitoring<PgSqlTestDbContext>();
            })
            .Build();

        // Create Quartz tables + job_executions before Quartz validates them
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PgSqlTestDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await host.StartAsync();
        await Task.Delay(400); // let Quartz finish initialising

        return new HostHandle(host, cleanup: async () =>
            await _fixture.DropSchemaAsync(schema));
    }

    protected override async Task<HostHandle> CreateClusteredHostAsync()
    {
        var schema = $"sch_{Guid.NewGuid():N}"[..16];

        await _fixture.CreateSchemaAsync(schema);

        var schemaOptions = new SchemaOptions(schema);
        var connStr       = _fixture.ConnectionString;

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(schemaOptions);

                services.AddDbContext<PgSqlTestDbContext>(opt =>
                    opt.UseNpgsql(connStr)
                       .ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>());

                services.AddPgSqlScheduler(
                    connectionString: connStr,
                    schema:           schema,
                    configure:        o => o.EnableClustering = true,
                    assemblies:       typeof(PostgreSqlSchedulerTests).Assembly);

                services.AddSchedulerMonitoring<PgSqlTestDbContext>();
            })
            .Build();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PgSqlTestDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await host.StartAsync();
        await Task.Delay(400);

        return new HostHandle(host, cleanup: async () =>
            await _fixture.DropSchemaAsync(schema));
    }
}
