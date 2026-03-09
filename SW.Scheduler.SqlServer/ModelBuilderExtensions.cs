using Microsoft.EntityFrameworkCore;
using SW.Scheduler.EfCore;
using SW.Scheduler.EfCore.EntityTypeConfigurations;

namespace SW.Scheduler.SqlServer;

/// <summary>
/// Extension for configuring Quartz + JobExecution tables in a SQL Server DbContext.
/// Delegates to the shared entity type configurations in SW.Scheduler.EfCore
/// using <see cref="QuartzColumnTypes.SqlServer"/> column types.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds Quartz.NET scheduler tables and the <c>job_executions</c> monitoring table
    /// to your DbContext for SQL Server.
    ///
    /// Call this in <c>OnModelCreating</c>:
    /// <code>
    /// modelBuilder.UseQuartzSqlServer();              // dbo schema, QRTZ_ prefix
    /// modelBuilder.UseQuartzSqlServer("scheduler");   // explicit schema
    /// </code>
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance.</param>
    /// <param name="schema">Database schema (optional, defaults to "dbo").</param>
    /// <param name="tablePrefix">Table prefix for Quartz tables (default: "QRTZ_").</param>
    public static ModelBuilder UseQuartzSqlServer(
        this ModelBuilder modelBuilder,
        string schema = "dbo",
        string tablePrefix = "QRTZ_")
    {
        if (string.IsNullOrWhiteSpace(tablePrefix))
            throw new ArgumentException("Table prefix cannot be empty", nameof(tablePrefix));

        return modelBuilder.ApplyScheduling(QuartzColumnTypes.SqlServer, schema, tablePrefix);
    }
}

