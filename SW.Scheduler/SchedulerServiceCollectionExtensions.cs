using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using SW.PrimitiveTypes;
using SW.Scheduler.Monitoring;

// Grant provider packages access to AddSchedulerCore so they can register
// all SW.Scheduler infrastructure without the in-memory Quartz store.
[assembly: InternalsVisibleTo("SW.Scheduler.PgSql")]
[assembly: InternalsVisibleTo("SW.Scheduler.SqlServer")]
[assembly: InternalsVisibleTo("SW.Scheduler.MySql")]

namespace SW.Scheduler;

public static class SchedulerServiceCollectionExtensions
{
    // -------------------------------------------------------------------------
    // Public entry points
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers SW.Scheduler with an in-memory (non-persistent) Quartz store.
    /// Use this for development, testing, or single-node deployments that don't
    /// need job persistence across restarts.
    ///
    /// For persistent storage pair with a provider-specific package instead:
    ///   services.AddPgSqlScheduler(...)   — SW.Scheduler.PgSql
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional callback to configure <see cref="SchedulerOptions"/>.</param>
    /// <param name="assemblies">
    /// Assemblies to scan for <see cref="IScheduledJob"/> and <see cref="IScheduledJob{TParam}"/> implementations.
    /// Defaults to the calling assembly when omitted.
    /// </param>
    public static IServiceCollection AddScheduler(
        this IServiceCollection services,
        Action<SchedulerOptions>? configureOptions = null,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0) assemblies = [Assembly.GetCallingAssembly()];

        // Register the no-op (in-memory) Quartz store — provider packages override this
        // by calling services.AddQuartz(...) with their own persistent store config AFTER
        // this method returns. Quartz's DI merges the configurations.
        services.AddQuartz(_ => { });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        // Register everything else (options, job scanning, monitoring, public APIs)
        AddSchedulerCore(services, configureOptions, assemblies);

        return services;
    }

    // -------------------------------------------------------------------------
    // Internal helper — called by AddScheduler AND by provider packages
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers all SW.Scheduler infrastructure <em>except</em> the Quartz store and
    /// hosted service. Provider packages (PgSql, SqlServer, MySql …) call this then add
    /// their own <c>AddQuartz</c> / <c>AddQuartzHostedService</c> on top.
    /// </summary>
    internal static IServiceCollection AddSchedulerCore(
        IServiceCollection services,
        Action<SchedulerOptions>? configureOptions,
        Assembly[] assemblies)
    {
        // ── Options ──────────────────────────────────────────────────────────
        var options = new SchedulerOptions();
        configureOptions?.Invoke(options);
        // Guard against double-registration (provider package may call this then AddScheduler)
        if (services.All(d => d.ServiceType != typeof(SchedulerOptions)))
            services.AddSingleton(options);

        // ── Job scanning ─────────────────────────────────────────────────────
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo<IScheduledJob>())
            .As<IScheduledJob>().AsSelf().WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(IScheduledJob<>)))
            .AsImplementedInterfaces().AsSelf().WithScopedLifetime());

        // ── Infrastructure ───────────────────────────────────────────────────
        if (services.All(d => d.ServiceType != typeof(JobsDiscovery)))
            services.AddSingleton<JobsDiscovery>();

        if (services.All(d => d.ServiceType != typeof(SchedulerPreparation)))
            services.AddHostedService<SchedulerPreparation>();

        // ── Monitoring ───────────────────────────────────────────────────────
        // Always registered; gracefully no-ops when IJobExecutionStore is absent.
        if (services.All(d => d.ServiceType != typeof(JobExecutionListener)))
            services.AddSingleton<JobExecutionListener>();

        // ── Public APIs ──────────────────────────────────────────────────────
        if (services.All(d => d.ServiceType != typeof(IScheduleRepository)))
            services.AddScoped<IScheduleRepository, ScheduleRepository>();

        if (services.All(d => d.ServiceType != typeof(IScheduleReader)))
            services.AddScoped<IScheduleReader, ScheduleReader>();

        return services;
    }
}
