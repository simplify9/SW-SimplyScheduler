using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.AdoJobStore;
using SW.Scheduler;

namespace SW.Scheduler.MySql;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SW.Scheduler with a MySQL/MariaDB-backed persistent store.
    ///
    /// In your DbContext's OnModelCreating call:
    ///   modelBuilder.UseSchedulerMySql()
    ///
    /// For job execution monitoring also call:
    ///   services.AddSchedulerMonitoring&lt;YourDbContext&gt;()
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">MySQL connection string.</param>
    /// <param name="configureOptions">Optional callback to configure <see cref="SchedulerOptions"/>.</param>
    /// <param name="configure">Optional MySQL-specific Quartz options.</param>
    /// <param name="assemblies">Assemblies to scan for scheduled jobs. Defaults to the calling assembly.</param>
    public static IServiceCollection AddMySqlScheduler(
        this IServiceCollection services,
        string connectionString,
        Action<SchedulerOptions>? configureOptions = null,
        Action<QuartzMySqlOptions>? configure = null,
        params Assembly[] assemblies)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        if (assemblies.Length == 0) assemblies = [Assembly.GetCallingAssembly()];

        var myOptions = new QuartzMySqlOptions { ConnectionString = connectionString };
        configure?.Invoke(myOptions);
        myOptions.Validate();

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

                s.UseMySqlConnector(mysql =>
                {
                    mysql.UseDriverDelegate<MySQLDelegate>();
                    mysql.ConnectionString = myOptions.ConnectionString;
                    mysql.TablePrefix = myOptions.TablePrefix;
                });

                s.UseSystemTextJsonSerializer();

                s.UseClustering(c =>
                {
                    c.CheckinMisfireThreshold = myOptions.ClusteringMisfireThreshold;
                    c.CheckinInterval = myOptions.ClusteringCheckinInterval;
                });
            });
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }

    /// <summary>
    /// Overload that configures all options via a single action.
    /// </summary>
    public static IServiceCollection AddMySqlScheduler(
        this IServiceCollection services,
        Action<QuartzMySqlOptions> configure,
        Action<SchedulerOptions>? configureOptions = null,
        params Assembly[] assemblies)
    {
        var myOptions = new QuartzMySqlOptions();
        configure(myOptions);
        myOptions.Validate();

        return services.AddMySqlScheduler(
            myOptions.ConnectionString,
            configureOptions,
            configure,
            assemblies);
    }
}

