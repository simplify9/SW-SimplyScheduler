using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SW.Scheduler.EfCore;
using SW.Scheduler.IntegrationTests.DbContexts;
using SW.Scheduler.IntegrationTests.Fixtures;
using SW.Scheduler.IntegrationTests.Infrastructure;
using SW.Scheduler.MySql;
using Xunit;

namespace SW.Scheduler.IntegrationTests.Tests;

/// <summary>
/// Runs the full test suite against a MySQL container.
/// Per-test isolation via separate databases (MySQL has no schema concept).
/// </summary>
[Collection("SchedulerIntegration")]
public class MySqlSchedulerTests : SchedulerTestBase, IClassFixture<MySqlFixture>
{
    private readonly MySqlFixture _fixture;

    public MySqlSchedulerTests(MySqlFixture fixture)
    {
        _fixture = fixture;
    }

    protected override async Task<HostHandle> CreateHostAsync()
    {
        var dbName = $"sched_{Guid.NewGuid():N}"[..20];

        await _fixture.CreateDatabaseAsync(dbName);

        var connStr = _fixture.ConnectionStringFor(dbName);

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<MySqlTestDbContext>(opt =>
                    opt.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

                services.AddMySqlScheduler(
                    connectionString: connStr,
                    assemblies:       typeof(MySqlSchedulerTests).Assembly);

                services.AddSchedulerMonitoring<MySqlTestDbContext>();
            })
            .Build();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MySqlTestDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await host.StartAsync();
        await Task.Delay(400);

        return new HostHandle(host, cleanup: async () =>
            await _fixture.DropDatabaseAsync(dbName));
    }
}
