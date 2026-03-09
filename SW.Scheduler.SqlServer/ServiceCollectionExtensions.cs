using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.AdoJobStore;
using SW.PrimitiveTypes;

namespace SW.Scheduler.SqlServer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SW.Scheduler with a SQL Server-backed Quartz persistent store.
    ///
    /// In your DbContext's OnModelCreating call:
    ///   modelBuilder.UseQuartzSqlServer()              // uses dbo schema
    ///   modelBuilder.UseQuartzSqlServer("scheduler")   // explicit schema
    ///
    /// For job execution monitoring also call:
    ///   services.AddSchedulerMonitoring&lt;YourDbContext&gt;()
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="configureOptions">Optional callback to configure <see cref="SchedulerOptions"/>.</param>
    /// <param name="configure">Optional SQL Server-specific Quartz options.</param>
    /// <param name="assemblies">Assemblies to scan for scheduled jobs. Defaults to the calling assembly.</param>
    public static IServiceCollection AddSqlServerScheduler(
        this IServiceCollection services,
        string connectionString,
        Action<SchedulerOptions>? configureOptions = null,
        Action<QuartzSqlServerOptions>? configure = null,
        params Assembly[] assemblies)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        if (assemblies.Length == 0) assemblies = [Assembly.GetCallingAssembly()];

        var sqlOptions = new QuartzSqlServerOptions { ConnectionString = connectionString };
        configure?.Invoke(sqlOptions);
        sqlOptions.Validate();

        SchedulerServiceCollectionExtensions.AddSchedulerCore(services, configureOptions, assemblies);

        services.AddQuartz(q =>
        {
            q.UsePersistentStore(s =>
            {
                s.PerformSchemaValidation = true;
                s.UseProperties = true;
                s.RetryInterval = TimeSpan.FromSeconds(15);

                s.UseSqlServer(sql =>
                {
                    sql.UseDriverDelegate<SqlServerDelegate>();
                    sql.ConnectionString = sqlOptions.ConnectionString;
                    // SQL Server table prefix includes schema: "dbo.QRTZ_"
                    sql.TablePrefix = $"{sqlOptions.Schema}.{sqlOptions.TablePrefix}";
                });

                s.UseSystemTextJsonSerializer();

                if (sqlOptions.EnableClustering)
                {
                    s.UseClustering(c =>
                    {
                        c.CheckinMisfireThreshold = sqlOptions.ClusteringMisfireThreshold;
                        c.CheckinInterval = sqlOptions.ClusteringCheckinInterval;
                    });
                }
            });
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }

    /// <summary>
    /// Overload that configures all options via a single action.
    /// </summary>
    public static IServiceCollection AddSqlServerScheduler(
        this IServiceCollection services,
        Action<QuartzSqlServerOptions> configure,
        Action<SchedulerOptions>? configureOptions = null,
        params Assembly[] assemblies)
    {
        var sqlOptions = new QuartzSqlServerOptions();
        configure(sqlOptions);
        sqlOptions.Validate();

        return services.AddSqlServerScheduler(
            sqlOptions.ConnectionString,
            configureOptions,
            configure,
            assemblies);
    }
}

