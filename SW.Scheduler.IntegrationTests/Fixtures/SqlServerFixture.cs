using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace SW.Scheduler.IntegrationTests.Fixtures;

/// <summary>
/// Starts a SQL Server Docker container once per test class.
/// Creates a dedicated test database and provides schema isolation per test.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    // Connection string targeting the scheduler_test database (not master)
    public string ConnectionString { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var masterConnStr = _container.GetConnectionString();

        // Create the application database
        await using (var conn = new SqlConnection(masterConnStr))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "IF DB_ID('scheduler_test') IS NULL CREATE DATABASE scheduler_test";
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new SqlConnectionStringBuilder(masterConnStr)
        {
            InitialCatalog = "scheduler_test"
        };
        ConnectionString = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public async Task CreateSchemaAsync(string schema)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // SQL Server schema names are limited to 128 chars; our generated names are ~12 chars
        cmd.CommandText =
            $"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{schema}') " +
            $"EXEC('CREATE SCHEMA [{schema}]')";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DropSchemaAsync(string schema)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            DECLARE @sql NVARCHAR(MAX) = N'';
            -- Drop FK constraints first to avoid dependency errors
            SELECT @sql += 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id)
                + '].[' + OBJECT_NAME(parent_object_id)
                + '] DROP CONSTRAINT [' + name + '];'
            FROM sys.foreign_keys
            WHERE OBJECT_SCHEMA_NAME(parent_object_id) = N'{schema}';
            EXEC sp_executesql @sql;
            SET @sql = N'';
            -- Now drop all tables in the schema
            SELECT @sql += 'DROP TABLE [' + TABLE_SCHEMA + '].[' + TABLE_NAME + '];'
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'{schema}';
            EXEC sp_executesql @sql;
            IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{schema}')
                EXEC('DROP SCHEMA [{schema}]')";
        await cmd.ExecuteNonQueryAsync();
    }
}
