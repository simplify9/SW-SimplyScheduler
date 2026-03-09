
namespace SW.Scheduler;

/// <summary>
/// Non-generic query service used by the Scheduler Admin UI (SW.Scheduler.Viewer).
/// Implemented by SW.Scheduler.EfCore — registered via <c>AddSchedulerMonitoring&lt;TDbContext&gt;()</c>.
/// Consumers only need to reference SW.Scheduler.Sdk (or SW.Scheduler.EfCore) — not the Viewer package.
/// </summary>
public interface ISchedulerViewerQuery
{
    Task<IReadOnlyList<JobExecution>> GetRunningAsync(CancellationToken ct = default);

    Task<IReadOnlyList<JobExecution>> GetRecentAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<JobExecution>> GetHistoryAsync(
        string? jobGroup,
        bool? success,
        int limit,
        CancellationToken ct = default);

    Task<JobExecution?> GetByFireInstanceIdAsync(string fireInstanceId, CancellationToken ct = default);
}

