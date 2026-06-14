using MySqlConnector;
using Testcontainers.MySql;
using Xunit;

namespace SW.Scheduler.IntegrationTests.Fixtures;

/// <summary>
/// Starts a MySQL Docker container once per test class.
/// Per-test isolation uses separate databases (MySQL has no schema concept).
/// MySQL is started with --lower-case-table-names=1 so Quartz's uppercase table
/// name queries match the lowercase names created by EF Core on a Linux host.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    // Testcontainers.MySql sets MYSQL_ROOT_PASSWORD to the same value as the user password
    private const string UserPassword = "testpass";

    // --lower-case-table-names=1 makes MySQL case-insensitive on Linux (Docker).
    // Quartz hardcodes uppercase names (QRTZ_JOB_DETAILS) while EF Core creates lowercase
    // names (QRTZ_job_details); with this flag both resolve identically.
    // Passed via CMD so it cannot be overridden by Testcontainers.MySql's my.cnf copy.
    private readonly MySqlContainer _container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("scheduler_main")
        .WithUsername("testuser")
        .WithPassword(UserPassword)
        .WithCommand("--lower-case-table-names=1")
        .Build();

    // Connection string using testuser (returned by GetConnectionString())
    private string _userConnectionString = default!;

    // Admin connection string using root (needed to CREATE/DROP databases)
    private string _adminConnectionString = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _userConnectionString = _container.GetConnectionString();

        // Build a root connection string to perform admin operations.
        // Testcontainers.MySql sets MYSQL_ROOT_PASSWORD == MYSQL_PASSWORD
        var b = new MySqlConnectionStringBuilder(_userConnectionString)
        {
            UserID = "root",
            Password = UserPassword
        };
        _adminConnectionString = b.ConnectionString;

        // Grant testuser global privileges so it can CREATE/DROP per-test databases
        await using var conn = new MySqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        await using var grantCmd = conn.CreateCommand();
        grantCmd.CommandText = "GRANT ALL PRIVILEGES ON *.* TO 'testuser'@'%' WITH GRANT OPTION";
        await grantCmd.ExecuteNonQueryAsync();
        grantCmd.CommandText = "FLUSH PRIVILEGES";
        await grantCmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Returns a connection string targeting a specific database name.
    /// </summary>
    public string ConnectionStringFor(string database)
    {
        var builder = new MySqlConnectionStringBuilder(_userConnectionString)
        {
            Database = database
        };
        return builder.ConnectionString;
    }

    public async Task CreateDatabaseAsync(string database)
    {
        await using var conn = new MySqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{database}`";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DropDatabaseAsync(string database)
    {
        await using var conn = new MySqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{database}`";
        await cmd.ExecuteNonQueryAsync();
    }
}
