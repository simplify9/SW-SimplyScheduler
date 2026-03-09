using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SW.Scheduler.Monitoring;

namespace SW.Scheduler.EfCore;

/// <summary>
/// Extension methods for wiring up the EF Core-backed job execution monitoring store.
/// </summary>
public static class SchedulerMonitoringExtensions
{
    /// <summary>
    /// Registers the EF Core-backed <see cref="IJobExecutionStore"/> using the supplied
    /// <typeparamref name="TDbContext"/>. The DbContext must expose a <c>DbSet&lt;JobExecution&gt;</c>
    /// by calling <c>modelBuilder.ApplyScheduling()</c> in <c>OnModelCreating</c>.
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
        // Scoped so that each request / job execution gets its own store instance
        // tied to the same DbContext scope.
        services.AddScoped<IJobExecutionStore, EfJobExecutionStore<TDbContext>>();

        return services;
    }
}

