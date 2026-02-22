using Microsoft.EntityFrameworkCore;
using SW.Scheduler.PgSql.EntityTypeConfigurations;

namespace SW.Scheduler.PgSql;

/// <summary>
/// Extensions for ModelBuilder to include Quartz.NET tables in your DbContext for PostgreSQL
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds Quartz.NET scheduler tables to your DbContext for PostgreSQL.
    /// Call this in your DbContext's OnModelCreating method.
    /// Schema is REQUIRED for PostgreSQL.
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance</param>
    /// <param name="schema">Database schema for Quartz tables (REQUIRED for PostgreSQL, e.g., "quartz")</param>
    /// <param name="tablePrefix">Table prefix for Quartz tables (default: "qrtz_")</param>
    /// <example>
    /// <code>
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     // Your entities
    ///     modelBuilder.Entity&lt;Customer&gt;()...
    ///     
    ///     // Add Quartz tables
    ///     modelBuilder.UseQuartzPostgreSql("quartz");
    /// }
    /// </code>
    /// </example>
    public static ModelBuilder UseQuartzPostgreSql(
        this ModelBuilder modelBuilder, 
        string schema,
        string tablePrefix = "qrtz_")
    {
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema is required for PostgreSQL Quartz tables", nameof(schema));

        if (string.IsNullOrWhiteSpace(tablePrefix))
            throw new ArgumentException("Table prefix cannot be empty", nameof(tablePrefix));

        // Apply all Quartz entity configurations
        ApplySchedulerPgSqlDbModels(modelBuilder, schema, tablePrefix);

        return modelBuilder;
    }

    /// <summary>
    /// Internal method to apply all Quartz entity configurations
    /// </summary>
    private static void ApplySchedulerPgSqlDbModels(
        ModelBuilder builder, 
        string schema,
        string prefix)
    {
        builder.ApplyConfiguration(
            new QuartzJobDetailEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzSimpleTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzSimplePropertyTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzCronTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzBlobTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzCalendarEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzPausedTriggerGroupEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzFiredTriggerEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzSchedulerStateEntityTypeConfiguration(prefix, schema));

        builder.ApplyConfiguration(
            new QuartzLockEntityTypeConfiguration(prefix, schema));
    }
}

