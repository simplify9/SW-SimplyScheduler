using Microsoft.EntityFrameworkCore;
using SW.Scheduler.EfCore;
using SW.Scheduler.EfCore.EntityTypeConfigurations;

namespace SW.Scheduler.PgSql;

/// <summary>
/// Extension for including scheduler tables in a PostgreSQL DbContext.
/// Delegates to the shared entity type configurations in SW.Scheduler.EfCore
/// using <see cref="QuartzColumnTypes.PostgreSql"/> column types.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds scheduler tables and the <c>job_executions</c> monitoring table
    /// to your DbContext for PostgreSQL.
    ///
    /// Call this in <c>OnModelCreating</c>:
    /// <code>
    /// modelBuilder.UseSchedulerPostgreSql("quartz");
    /// </code>
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance.</param>
    /// <param name="schema">Database schema (required for PostgreSQL, e.g. "quartz").</param>
    /// <param name="tablePrefix">Table prefix for scheduler tables (default: "qrtz_").</param>
    public static ModelBuilder UseSchedulerPostgreSql(
        this ModelBuilder modelBuilder,
        string schema,
        string tablePrefix = "qrtz_")
    {
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema is required for PostgreSQL scheduler tables", nameof(schema));
        if (string.IsNullOrWhiteSpace(tablePrefix))
            throw new ArgumentException("Table prefix cannot be empty", nameof(tablePrefix));

        return modelBuilder.ApplyScheduling(QuartzColumnTypes.PostgreSql, schema, tablePrefix);
    }
}
