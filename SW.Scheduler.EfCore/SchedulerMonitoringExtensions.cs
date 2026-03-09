using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SW.Scheduler;
using SW.Scheduler.Monitoring;

namespace SW.Scheduler.EfCore;

/// <summary>
/// Extension methods for wiring up the EF Core-backed job execution monitoring store.
/// </summary>
public static class SchedulerMonitoringExtensions
{
    /// <summary>
    /// Registers the EF Core-backed <see cref="IJobExecutionStore"/> using the supplied
    /// <typeparamref name="TDbContext"/>, and also registers
    /// <see cref="ISchedulerViewerQuery"/> so the Scheduler Admin UI (SW.Scheduler.Viewer)
    /// can query execution history without knowing concrete job types at compile time.
    ///
    /// The DbContext must call <c>modelBuilder.ApplyScheduling()</c> in
    /// <c>OnModelCreating</c> to include the <c>job_executions</c> table.
    /// </summary>
    /// <typeparam name="TDbContext">The application's EF Core DbContext.</typeparam>
    /// <example>
    /// <code>
    /// // Program.cs
    /// builder.Services.AddScheduler(o => { o.EnableArchive = true; });
    /// builder.Services.AddSchedulerMonitoring&lt;AppDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddSchedulerMonitoring<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        // Scoped so each request/job execution gets its own store tied to the same DbContext scope.
        services.AddScoped<IJobExecutionStore, EfJobExecutionStore<TDbContext>>();

        // ISchedulerViewerQuery — used by SW.Scheduler.Viewer admin UI.
        // Safe no-op if Viewer package is not installed (interface will simply not be resolved).
        services.AddScoped<ISchedulerViewerQuery, EfCoreSchedulerViewerQuery<TDbContext>>();

        return services;
    }
}
