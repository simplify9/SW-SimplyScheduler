namespace SW.PrimitiveTypes;

/// <summary>
/// Runtime configuration for a specific job schedule.
/// Passed to <see cref="IScheduleRepository"/> methods to override the defaults
/// declared on the job class via <see cref="ScheduleConfigAttribute"/> and <see cref="RetryConfigAttribute"/>.
/// </summary>
public class ScheduleConfig
{
    /// <summary>
    /// Whether multiple instances of this schedule may run concurrently.
    /// Default: <c>false</c>.
    /// </summary>
    public bool AllowConcurrentExecution { get; set; } = false;

    /// <summary>
    /// Whether Quartz should attempt to re-execute the job after a scheduler crash or restart.
    /// Default: <c>true</c>.
    /// </summary>
    public bool RequestsRecovery { get; set; } = true;

    /// <summary>
    /// Behaviour when a trigger misfires.
    /// Default: <see cref="MisfireInstructions.FireOnce"/>.
    /// </summary>
    public MisfireInstructions MisfireInstructions { get; set; } = MisfireInstructions.FireOnce;

    // -------------------------------------------------------------------------
    // Retry strategy (self-rescheduling pattern)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enables the self-rescheduling retry strategy.
    /// When <c>true</c> and a job execution throws, the scheduler will:
    /// <list type="number">
    ///   <item>Catch the exception and save the error details to the job's data map.</item>
    ///   <item>Increment a persistent <c>RetryCount</c> in the job's data map.</item>
    ///   <item>If <c>RetryCount &lt; MaxRetries</c>, create a new one-time trigger at <c>now + RetryAfterMinutes</c>.</item>
    ///   <item>Let the current execution finish normally (no Quartz error state).</item>
    /// </list>
    /// Once <c>MaxRetries</c> is exhausted the failure is logged and no further retry is scheduled.
    /// Default: <c>false</c>.
    /// </summary>
    public bool EnableRetry { get; set; } = false;

    /// <summary>
    /// Maximum number of retry attempts after the initial failure.
    /// Only used when <see cref="EnableRetry"/> is <c>true</c>.
    /// Default: <c>3</c>.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// How many minutes to wait before each retry attempt.
    /// Only used when <see cref="EnableRetry"/> is <c>true</c>.
    /// Default: <c>5</c> minutes.
    /// </summary>
    public double RetryAfterMinutes { get; set; } = 5;
}
