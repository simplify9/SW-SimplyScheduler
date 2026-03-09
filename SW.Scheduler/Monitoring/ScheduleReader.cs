#nullable enable
using SW.Scheduler;

namespace SW.Scheduler.Monitoring;

/// <summary>
/// Implementation of <see cref="IScheduleReader"/> backed by <see cref="IJobExecutionStore"/>.
/// Queries are constrained to the configured retention window.
/// </summary>
internal sealed class ScheduleReader(IJobExecutionStore store, SchedulerOptions options, JobsDiscovery jobsDiscovery)
    : IScheduleReader
{
    // ── Simple jobs ───────────────────────────────────────────────────────────

    public async Task<JobExecution?> GetLastExecution<TJob>()
        where TJob : IScheduledJob
    {
        var def  = RequireDefinition(typeof(TJob));
        var rows = await store.QueryAsync(def.Group, JobKeyConventions.MainJobName,
            successFilter: null, since: RetentionCutoff(), runningOnly: false, limit: 1);
        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<JobExecution>> GetRecentExecutions<TJob>(int limit = 20)
        where TJob : IScheduledJob
    {
        var def = RequireDefinition(typeof(TJob));
        return await store.QueryAsync(def.Group, JobKeyConventions.MainJobName,
            successFilter: null, since: RetentionCutoff(), runningOnly: false, limit: limit);
    }

    public async Task<IReadOnlyList<JobExecution>> GetFailedExecutions<TJob>(DateTime? since = null)
        where TJob : IScheduledJob
    {
        var def = RequireDefinition(typeof(TJob));
        return await store.QueryAsync(def.Group, JobKeyConventions.MainJobName,
            successFilter: false, since: since ?? RetentionCutoff(), runningOnly: false, limit: null);
    }

    // ── Parameterized jobs ────────────────────────────────────────────────────

    public async Task<JobExecution?> GetLastExecution<TJob, TParam>(string scheduleKey)
        where TJob : IScheduledJob<TParam>
    {
        var def  = RequireDefinition(typeof(TJob));
        var rows = await store.QueryAsync(def.Group, scheduleKey,
            successFilter: null, since: RetentionCutoff(), runningOnly: false, limit: 1);
        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<JobExecution>> GetRecentExecutions<TJob, TParam>(string scheduleKey, int limit = 20)
        where TJob : IScheduledJob<TParam>
    {
        var def = RequireDefinition(typeof(TJob));
        return await store.QueryAsync(def.Group, scheduleKey,
            successFilter: null, since: RetentionCutoff(), runningOnly: false, limit: limit);
    }

    public async Task<IReadOnlyList<JobExecution>> GetFailedExecutions<TJob, TParam>(string scheduleKey, DateTime? since = null)
        where TJob : IScheduledJob<TParam>
    {
        var def = RequireDefinition(typeof(TJob));
        return await store.QueryAsync(def.Group, scheduleKey,
            successFilter: false, since: since ?? RetentionCutoff(), runningOnly: false, limit: null);
    }

    // ── Cross-job ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<JobExecution>> GetRunningExecutions()
        => await store.QueryAsync(jobGroup: null!, jobName: null!,
            successFilter: null, since: null, runningOnly: true, limit: null);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DateTime RetentionCutoff() => DateTime.UtcNow.AddDays(-options.RetentionDays);

    private ScheduledJobDefinition RequireDefinition(Type jobType)
    {
        var def = jobsDiscovery.GetJobDefinition(jobType);
        if (def == null)
            throw new InvalidOperationException(
                $"Job '{jobType.Name}' is not registered. Ensure it is added via AddScheduler().");
        return def;
    }
}


