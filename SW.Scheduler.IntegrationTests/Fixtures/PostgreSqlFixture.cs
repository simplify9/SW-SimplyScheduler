using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace SW.Scheduler.IntegrationTests.Fixtures;

/// <summary>
/// Starts a PostgreSQL Docker container once per test class.
/// Provides helpers to create and drop per-test schemas for isolation.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("scheduler_test")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    public string ConnectionString { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public async Task CreateSchemaAsync(string schema)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DropSchemaAsync(string schema)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
        await cmd.ExecuteNonQueryAsync();
    }
}
