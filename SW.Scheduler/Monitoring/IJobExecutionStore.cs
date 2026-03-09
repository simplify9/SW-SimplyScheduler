#nullable enable
using SW.PrimitiveTypes;

namespace SW.Scheduler.Monitoring;

/// <summary>
/// Abstraction over the DbContext-backed store for <see cref="JobExecution"/> records.
/// Implemented by the EF Core layer (SW.Scheduler.EfCore) and registered by the user's app
/// via <c>AddSchedulerMonitoring&lt;TDbContext&gt;()</c>.
/// </summary>
public interface IJobExecutionStore
{
    Task InsertAsync(JobExecution record, CancellationToken ct = default);

    Task UpdateAsync(
        string fireInstanceId,
        DateTime endTimeUtc,
        long durationMs,
        bool success,
        string? error,
        CancellationToken ct = default);

    Task<List<JobExecution>> QueryAsync(
        string jobGroup,
        string jobName,
        bool? successFilter,
        DateTime? since,
        bool runningOnly,
        int? limit,
        CancellationToken ct = default);
}

/// <summary>
/// Optional extension to <see cref="IJobExecutionStore"/> for efficient bulk-delete.
/// Implemented by the EF Core store.
/// </summary>
public interface IJobExecutionCleanupStore
{
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}



