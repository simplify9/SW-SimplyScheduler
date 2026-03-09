namespace SW.Scheduler;

/// <summary>
/// Options for the SW.Scheduler library. Pass to <c>AddScheduler(options => ...)</c>.
/// </summary>
public class SchedulerOptions
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The identifier used as the system user when setting up the <c>RequestContext</c>
    /// for each job execution. Appears as the <c>NameIdentifier</c> claim and
    /// the <c>GenericIdentity</c> name.
    /// Default: <c>"scheduled-job"</c>.
    /// </summary>
    public string SystemUserIdentifier { get; set; } = "scheduled-job";
    // ── Execution history / monitoring ────────────────────────────────────────

    /// <summary>
    /// How many days to keep <see cref="JobExecution"/> rows in the RDBMS before the
    /// cleanup job deletes them.
    /// Default: <c>30</c>.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Cron expression that controls how often the cleanup job runs.
    /// Default: <c>"0 0 2 * * ?"</c> (every day at 02:00 AM server time).
    /// </summary>
    public string CleanupCronExpression { get; set; } = "0 0 2 * * ?";

    // ── Cloud archiving ───────────────────────────────────────────────────────

    /// <summary>
    /// When <c>true</c>, each completed execution is serialised to JSON and uploaded via
    /// <c>ICloudFilesService</c> (from <c>SimplyWorks.PrimitiveTypes</c>) under:
    /// <c>{CloudFilesPrefix}job-history/{JobGroup}/{yyyy}/{MM}/{dd}/{FireInstanceId}.json</c>.
    /// Default: <c>false</c>.
    /// </summary>
    public bool EnableArchive { get; set; } = false;

    /// <summary>
    /// Optional prefix that is prepended to every cloud storage key.
    /// E.g. <c>"my-app/"</c> → <c>"my-app/job-history/..."</c>.
    /// Only used when <see cref="EnableArchive"/> is <c>true</c>.
    /// </summary>
    public string CloudFilesPrefix { get; set; } = string.Empty;
}

