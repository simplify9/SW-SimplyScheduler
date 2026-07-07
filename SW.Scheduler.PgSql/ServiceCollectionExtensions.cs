using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.AdoJobStore;
using SW.Scheduler;

namespace SW.Scheduler.PgSql;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SW.Scheduler with a PostgreSQL-backed persistent store.
    ///
    /// In your DbContext's OnModelCreating call:
    ///   modelBuilder.UseSchedulerPostgreSql("quartz")
    ///
    /// For job execution monitoring also call:
    ///   services.AddSchedulerMonitoring&lt;YourDbContext&gt;()
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="schema">Database schema for Quartz tables (required, e.g. "quartz").</param>
    /// <param name="configureOptions">Optional callback to configure <see cref="SchedulerOptions"/>.</param>
    /// <param name="configure">Optional PostgreSQL-specific Quartz options.</param>
    /// <param name="assemblies">Assemblies to scan for scheduled jobs. Defaults to the calling assembly.</param>
    public static IServiceCollection AddPgSqlScheduler(
        this IServiceCollection services,
        string connectionString,
        string schema,
        Action<SchedulerOptions>? configureOptions = null,
        Action<QuartzPgSqlOptions>? configure = null,
        params Assembly[] assemblies)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema is required for PostgreSQL", nameof(schema));

        if (assemblies.Length == 0) assemblies = [Assembly.GetCallingAssembly()];

        var pgOptions = new QuartzPgSqlOptions { ConnectionString = connectionString, Schema = schema };
        configure?.Invoke(pgOptions);
        pgOptions.Validate();

        SchedulerServiceCollectionExtensions.AddSchedulerCore(services, configureOptions, assemblies);

        services.AddQuartz(q =>
        {
            // Clustering is always on for persistent stores: it's the only way locks/triggers
            // are acquired safely across processes, and it enables recovery of orphaned
            // locks/triggers after an unclean shutdown even with a single instance running.
            // Each clustered node must have a unique scheduler instance identity; the Quartz
            // default ("NON_CLUSTERED") is shared by every process, which breaks cluster
            // coordination (nodes recover each other's locks/triggers instead of their own).
            q.SchedulerId = "AUTO";

            q.UsePersistentStore(s =>
            {
                s.PerformSchemaValidation = true;
                s.UseProperties = true;
                s.RetryInterval = TimeSpan.FromSeconds(15);
                s.UsePostgres(pg =>
                {
                    pg.UseDriverDelegate<PostgreSQLDelegate>();
                    pg.ConnectionString = pgOptions.ConnectionString;
                    pg.TablePrefix = $"{pgOptions.Schema}.{pgOptions.TablePrefix}";
                });
                s.UseSystemTextJsonSerializer();
                s.UseClustering(c =>
                {
                    c.CheckinMisfireThreshold = pgOptions.ClusteringMisfireThreshold;
                    c.CheckinInterval = pgOptions.ClusteringCheckinInterval;
                });
            });
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }

    /// <summary>
    /// Overload that configures connection string and schema via an options action.
    /// </summary>
    public static IServiceCollection AddPgSqlScheduler(
        this IServiceCollection services,
        Action<QuartzPgSqlOptions> configure,
        Action<SchedulerOptions>? configureOptions = null,
        params Assembly[] assemblies)
    {
        var pgOptions = new QuartzPgSqlOptions();
        configure(pgOptions);
        pgOptions.Validate();

        return services.AddPgSqlScheduler(
            pgOptions.ConnectionString,
            pgOptions.Schema,
            configureOptions,
            configure,
            assemblies);
    }
}

