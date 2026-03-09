namespace SW.PrimitiveTypes;

/// <summary>
/// Provides read-only dashboard queries over the job execution history stored in the RDBMS.
/// Only covers the last N days (configured by <see cref="SchedulerOptions.RetentionDays"/>).
/// </summary>
public interface IScheduleReader
{
    // ── Simple jobs (no params) ───────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent execution record for a simple (non-parameterized) job,
    /// or <c>null</c> if it has never run.
    /// </summary>
    Task<JobExecution?> GetLastExecution<TJob>()
        where TJob : IScheduledJob;

    /// <summary>
    /// Returns the <paramref name="limit"/> most recent executions for a simple job,
    /// newest first.
    /// </summary>
    Task<IReadOnlyList<JobExecution>> GetRecentExecutions<TJob>(int limit = 20)
        where TJob : IScheduledJob;

    /// <summary>
    /// Returns all failed executions for a simple job since <paramref name="since"/> (UTC).
    /// </summary>
    Task<IReadOnlyList<JobExecution>> GetFailedExecutions<TJob>(DateTime? since = null)
        where TJob : IScheduledJob;

    // ── Parameterized jobs ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent execution record for a specific schedule of a parameterized job,
    /// identified by <paramref name="scheduleKey"/>.
    /// </summary>
    Task<JobExecution?> GetLastExecution<TJob, TParam>(string scheduleKey)
        where TJob : IScheduledJob<TParam>;

    /// <summary>
    /// Returns the <paramref name="limit"/> most recent executions for a specific schedule
    /// of a parameterized job, newest first.
    /// </summary>
    Task<IReadOnlyList<JobExecution>> GetRecentExecutions<TJob, TParam>(string scheduleKey, int limit = 20)
        where TJob : IScheduledJob<TParam>;

    /// <summary>
    /// Returns all failed executions for a specific schedule of a parameterized job
    /// since <paramref name="since"/> (UTC).
    /// </summary>
    Task<IReadOnlyList<JobExecution>> GetFailedExecutions<TJob, TParam>(string scheduleKey, DateTime? since = null)
        where TJob : IScheduledJob<TParam>;

    // ── Cross-job queries ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all executions that are currently in-progress (started but not yet finished),
    /// across all job types and nodes.
    /// </summary>
    Task<IReadOnlyList<JobExecution>> GetRunningExecutions();
}

