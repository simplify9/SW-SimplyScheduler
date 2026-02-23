namespace SW.PrimitiveTypes;

/// <summary>
/// Runtime configuration for a specific job schedule.
/// Passed to <see cref="IScheduleRepository"/> methods to override the defaults
/// declared on the job class via <see cref="ScheduleConfigAttribute"/>.
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
}

