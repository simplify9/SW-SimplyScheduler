using Microsoft.EntityFrameworkCore;
using SW.Scheduler.EfCore;
using SW.Scheduler.EfCore.EntityTypeConfigurations;

namespace SW.Scheduler.MySql;

/// <summary>
/// Extension for including scheduler tables in a MySQL/MariaDB DbContext.
/// Delegates to the shared entity type configurations in SW.Scheduler.EfCore
/// using <see cref="QuartzColumnTypes.MySql"/> column types.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds scheduler tables and the <c>job_executions</c> monitoring table
    /// to your DbContext for MySQL/MariaDB.
    ///
    /// Call this in <c>OnModelCreating</c>:
    /// <code>
    /// modelBuilder.UseSchedulerMySql()          // default prefix
    /// modelBuilder.UseSchedulerMySql("QRTZ_")   // explicit prefix
    /// </code>
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance.</param>
    /// <param name="tablePrefix">Table prefix for scheduler tables (default: "QRTZ_").</param>
    public static ModelBuilder UseSchedulerMySql(
        this ModelBuilder modelBuilder,
        string tablePrefix = "QRTZ_")
    {
        if (string.IsNullOrWhiteSpace(tablePrefix))
            throw new ArgumentException("Table prefix cannot be empty", nameof(tablePrefix));

        // MySQL doesn't use schemas the same way — pass null for schema
        return modelBuilder.ApplyScheduling(QuartzColumnTypes.MySql, schema: null, tablePrefix);
    }
}

