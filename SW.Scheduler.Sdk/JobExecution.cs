using System.Text.Json;

namespace SW.PrimitiveTypes;

/// <summary>
/// Holds contextual information captured at the moment a job execution fires.
/// Serialized to JSON and stored in the <c>context</c> column of <c>job_executions</c>.
/// </summary>
public class ScheduledJobContext
{
    /// <summary>
    /// The parameter object passed to the job at scheduling time.
    /// For simple (non-parameterized) jobs this will be <c>null</c>.
    /// Serialized as-is using System.Text.Json.
    /// </summary>
    public object? JobParameter { get; set; }
}

/// <summary>
/// Represents a single execution record of a scheduled job.
/// Stored in the RDBMS for 30 days (configurable) and archived to cloud storage.
/// </summary>
public class JobExecution
{
    /// <summary>Auto-incremented primary key.</summary>
    public long Id { get; set; }

    // ── Job identifier fields ─────────────────────────────────────────────────

    /// <summary>
    /// The Quartz <c>JobKey.Name</c>. For simple jobs this is always <c>"MAIN"</c>;
    /// for parameterized jobs it is the <c>scheduleKey</c> chosen by the caller.
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// The Quartz <c>JobKey.Group</c>: last namespace segment + class name,
    /// e.g. <c>"Jobs.SendCustomerEmailsJob"</c>.
    /// </summary>
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>
    /// The simple class name of the job (e.g. <c>"SendCustomerEmailsJob"</c>).
    /// Derived from <see cref="JobGroup"/> for convenience.
    /// </summary>
    public string JobTypeName { get; set; } = string.Empty;

    // ── Execution identity ────────────────────────────────────────────────────

    /// <summary>
    /// Quartz fire-instance ID. Unique per execution across clustered nodes.
    /// Used to correlate <c>JobToBeExecuted</c> and <c>JobWasExecuted</c> events.
    /// </summary>
    public string FireInstanceId { get; set; } = string.Empty;

    // ── Timing ───────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when the job started.</summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>UTC timestamp when the job finished. <c>null</c> while running.</summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>Execution duration in milliseconds. <c>null</c> while running.</summary>
    public long? DurationMs { get; set; }

    // ── Outcome ───────────────────────────────────────────────────────────────

    /// <summary><c>true</c> if the job completed without throwing. <c>null</c> while running.</summary>
    public bool? Success { get; set; }

    /// <summary>Exception message and type if the job failed. <c>null</c> on success or while running.</summary>
    public string? Error { get; set; }

    // ── Cluster info ──────────────────────────────────────────────────────────

    /// <summary>
    /// Machine/container name that ran this execution (<see cref="Environment.MachineName"/>).
    /// Useful in clustered environments to identify which node ran the job.
    /// </summary>
    public string Node { get; set; } = string.Empty;

    // ── Execution context ─────────────────────────────────────────────────────

    /// <summary>
    /// Contextual snapshot captured when the job fired.
    /// Contains <see cref="ScheduledJobContext.JobParameter"/> — the parameter object
    /// passed to a parameterized job (null for simple jobs).
    /// Stored as a JSON text column; deserialized on read.
    /// </summary>
    public string? Context { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        WriteIndented               = false
    };

    /// <summary>
    /// Serializes a <see cref="ScheduledJobContext"/> and assigns the result to <see cref="Context"/>.
    /// </summary>
    public void SetContext(ScheduledJobContext ctx)
        => Context = JsonSerializer.Serialize(ctx, _jsonOptions);

    /// <summary>
    /// Deserializes <see cref="Context"/> back into a <see cref="ScheduledJobContext"/>.
    /// Returns <c>null</c> when <see cref="Context"/> is empty.
    /// </summary>
    public ScheduledJobContext? GetContext()
        => string.IsNullOrWhiteSpace(Context)
            ? null
            : JsonSerializer.Deserialize<ScheduledJobContext>(Context, _jsonOptions);
}

