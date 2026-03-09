namespace SW.PrimitiveTypes;

/// <summary>
/// Non-generic command service used by the Scheduler Admin UI (SW.Scheduler.Viewer).
/// Allows the UI to pause, resume, reschedule and unschedule any job by its
/// Quartz group/name strings — without needing the CLR job type at compile time.
///
/// Registered automatically by <c>AddScheduler()</c> / provider packages.
/// </summary>
public interface ISchedulerViewerCommand
{
    /// <summary>
    /// Returns a snapshot of every Quartz job key known to the scheduler,
    /// along with each trigger's current state.
    /// </summary>
    Task<IReadOnlyList<JobSummary>> GetAllJobsAsync(CancellationToken ct = default);

    /// <summary>Pauses all triggers for the job identified by <paramref name="group"/> and <paramref name="name"/>.</summary>
    Task PauseAsync(string group, string name, CancellationToken ct = default);

    /// <summary>Resumes all triggers for the job identified by <paramref name="group"/> and <paramref name="name"/>.</summary>
    Task ResumeAsync(string group, string name, CancellationToken ct = default);

    /// <summary>
    /// Replaces the cron expression on the first cron trigger found for the job.
    /// For simple jobs this is the single MAIN trigger.
    /// For parameterized jobs (schedule-key = job name) this updates that key's trigger.
    /// </summary>
    Task RescheduleAsync(string group, string name, string newCronExpression, CancellationToken ct = default);

    /// <summary>
    /// Removes all triggers for the job. The durable job detail is kept so the job
    /// can be re-triggered later. For parameterized jobs the dedicated Quartz job is deleted.
    /// </summary>
    Task UnscheduleAsync(string group, string name, CancellationToken ct = default);
}

/// <summary>Snapshot of a single Quartz job and its trigger state.</summary>
public class JobSummary
{
    public string Group    { get; init; } = "";
    public string Name     { get; init; } = "";

    /// <summary>
    /// Human-readable job type name derived from the group
    /// (last segment after the final dot, e.g. "Jobs.SendCustomerEmailsJob" → "SendCustomerEmailsJob").
    /// </summary>
    public string TypeName { get; init; } = "";

    /// <summary><c>true</c> when the job name is not "MAIN" — i.e. it was created by a parameterized schedule.</summary>
    public bool IsParameterized { get; init; }

    /// <summary>
    /// Quartz trigger state string: "Normal", "Paused", "Complete", "Error", "Blocked", "None".
    /// </summary>
    public string TriggerState { get; init; } = "None";

    /// <summary>Next scheduled fire time (UTC), or <c>null</c> when paused / unscheduled.</summary>
    public DateTimeOffset? NextFireTime { get; init; }

    /// <summary>Previous fire time (UTC), or <c>null</c> if never fired.</summary>
    public DateTimeOffset? PreviousFireTime { get; init; }

    /// <summary>The cron expression currently on the trigger, or <c>null</c> for one-time triggers.</summary>
    public string? CronExpression { get; init; }
}

