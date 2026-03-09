using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using SW.PrimitiveTypes;

namespace SW.Scheduler.Monitoring;

/// <summary>
/// Quartz job that deletes <see cref="JobExecution"/> rows older than
/// <see cref="SchedulerOptions.RetentionDays"/> days.
/// Registered automatically by <c>AddScheduler()</c>.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class JobExecutionCleanupJob(
    IServiceProvider serviceProvider,
    SchedulerOptions options,
    ILogger<JobExecutionCleanupJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetService<IJobExecutionStore>();
            if (store == null)
            {
                logger.LogDebug("[Cleanup] IJobExecutionStore not registered — skipping.");
                return;
            }

            var cutoff  = DateTime.UtcNow.AddDays(-options.RetentionDays);
            var deleted = await DeleteOlderThanAsync(store, cutoff, context.CancellationToken);

            logger.LogInformation(
                "[Cleanup] Deleted {Count} job execution record(s) older than {Cutoff:yyyy-MM-dd} (retention={Days}d).",
                deleted, cutoff, options.RetentionDays);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Cleanup] Job execution cleanup failed.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    private static async Task<int> DeleteOlderThanAsync(IJobExecutionStore store, DateTime cutoff, CancellationToken ct)
    {
        if (store is IJobExecutionCleanupStore cleanupStore)
            return await cleanupStore.DeleteOlderThanAsync(cutoff, ct);
        return 0;
    }
}


