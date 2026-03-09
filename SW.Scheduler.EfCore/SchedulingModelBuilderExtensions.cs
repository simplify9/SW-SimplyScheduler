using Microsoft.EntityFrameworkCore;
using SW.Scheduler;
using SW.Scheduler.EfCore.EntityTypeConfigurations;

namespace SW.Scheduler.EfCore;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> that add all SW.Scheduler entity
/// configurations (Quartz tables + <see cref="JobExecution"/>) to the user's DbContext.
///
/// Call this in <c>OnModelCreating</c>:
/// <code>
/// modelBuilder.ApplyScheduling();                        // provider-agnostic defaults
/// modelBuilder.ApplyScheduling(schema: "quartz");        // explicit schema
/// modelBuilder.ApplyScheduling(QuartzColumnTypes.PostgreSql, "quartz");  // explicit types + schema
/// </code>
///
/// Provider packages (SW.Scheduler.PgSql, SW.Scheduler.SqlServer, SW.Scheduler.MySql)
/// call the overload with their own <see cref="QuartzColumnTypes"/> preset.
/// </summary>
public static class SchedulingModelBuilderExtensions
{
    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Applies all Quartz entity configurations and the <see cref="JobExecution"/> monitoring
    /// table using provider-agnostic defaults (no schema, generic column types).
    /// Suitable for in-memory, SQLite, and any EF Core provider that infers column types.
    /// </summary>
    public static ModelBuilder ApplyScheduling(this ModelBuilder modelBuilder)
        => modelBuilder.ApplyScheduling(columnTypes: null, schema: null, tablePrefix: "qrtz_");

    /// <summary>
    /// Applies all Quartz entity configurations and <see cref="JobExecution"/> with an explicit schema.
    /// Column types are inferred by EF Core.
    /// </summary>
    public static ModelBuilder ApplyScheduling(this ModelBuilder modelBuilder, string? schema, string tablePrefix = "qrtz_")
        => modelBuilder.ApplyScheduling(columnTypes: null, schema: schema, tablePrefix: tablePrefix);

    /// <summary>
    /// Applies all Quartz entity configurations and <see cref="JobExecution"/> with explicit
    /// column types and optional schema. Used by provider packages.
    /// </summary>
    public static ModelBuilder ApplyScheduling(
        this ModelBuilder modelBuilder,
        QuartzColumnTypes? columnTypes,
        string? schema,
        string tablePrefix = "qrtz_")
    {
        var types = columnTypes ?? new QuartzColumnTypes(); // defaults = PostgreSQL-compatible

        // ── Quartz tables ─────────────────────────────────────────────────────
        modelBuilder.ApplyConfiguration(new QuartzJobDetailEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzTriggerEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzSimpleTriggerEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzSimplePropertyTriggerEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzCronTriggerEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzBlobTriggerEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzCalendarEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzPausedTriggerGroupEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzFiredTriggerEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzSchedulerStateEntityTypeConfiguration(tablePrefix, schema, types));
        modelBuilder.ApplyConfiguration(new QuartzLockEntityTypeConfiguration(tablePrefix, schema, types));

        // ── JobExecution monitoring table ─────────────────────────────────────
        modelBuilder.ApplyConfiguration(new JobExecutionEntityTypeConfiguration(schema, types));

        return modelBuilder;
    }
}
