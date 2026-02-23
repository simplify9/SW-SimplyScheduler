namespace SW.PrimitiveTypes;

/// <summary>
/// Declaratively schedules an <see cref="IScheduledJob"/> on application startup using a cron expression.
/// The schedule can be overridden at runtime via <see cref="IScheduleRepository.Schedule{TScheduler}"/>.
/// <para>Only applicable to <see cref="IScheduledJob"/> (non-parameterized) implementations.</para>
/// </summary>
/// <example>
/// <code>
/// [Schedule("0 0 * * * ?", Description = "Runs every hour")]
/// public class MyJob : IScheduledJob { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ScheduleAttribute : Attribute
{
    /// <summary>
    /// Cron expression defining when the job should run.
    /// Uses Quartz.NET cron format (6 or 7 fields).
    /// <example><c>"0 0 * * * ?"</c> — every hour on the hour.</example>
    /// </summary>
    public string CronExpression { get; }

    /// <summary>Optional human-readable description stored on the Quartz trigger.</summary>
    public string? Description { get; set; }

    /// <param name="cronExpression">A valid Quartz.NET cron expression.</param>
    public ScheduleAttribute(string cronExpression)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
    }
}

/// <summary>
/// Declaratively configures execution behaviour for a scheduled job.
/// Controls concurrency, crash-recovery, and misfire handling.
/// When omitted, defaults apply: no concurrency, recovery enabled, misfire fires once.
/// <para>
/// Applicable to both <see cref="IScheduledJob"/> and <see cref="IScheduledJob{TParam}"/> implementations.
/// At runtime, these defaults can be overridden per-schedule via <see cref="ScheduleConfig"/>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [ScheduleConfig(AllowConcurrentExecution = true, MisfireInstructions = MisfireInstructions.Skip)]
/// public class MyJob : IScheduledJob&lt;MyParams&gt; { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ScheduleConfigAttribute : Attribute
{
    /// <summary>
    /// Whether multiple instances of this job may run at the same time.
    /// Default: <c>false</c> — only one instance runs at a time.
    /// </summary>
    public bool AllowConcurrentExecution { get; set; } = false;

    /// <summary>
    /// Whether Quartz should attempt to re-execute this job after a scheduler crash or restart.
    /// Default: <c>true</c>.
    /// </summary>
    public bool RequestsRecovery { get; set; } = true;

    /// <summary>
    /// Behaviour when a trigger fires but the scheduled time has already passed.
    /// Default: <see cref="MisfireInstructions.FireOnce"/>.
    /// </summary>
    public MisfireInstructions MisfireInstructions { get; set; } = MisfireInstructions.FireOnce;
}

