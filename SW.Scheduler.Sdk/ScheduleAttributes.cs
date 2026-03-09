namespace SW.Scheduler;

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
    /// Uses 6-field cron format: <c>second minute hour dayOfMonth month dayOfWeek</c>.
    /// <example><c>"0 0 * * * ?"</c> — every hour on the hour.</example>
    /// </summary>
    public string CronExpression { get; }

    /// <summary>Optional human-readable description stored alongside the trigger.</summary>
    public string? Description { get; set; }

    /// <param name="cronExpression">A valid Quartz.NET cron expression.</param>
    public ScheduleAttribute(string cronExpression)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
    }
}

/// <summary>
/// Declaratively configures the self-rescheduling retry strategy for a scheduled job.
/// When a job execution throws an exception the scheduler will catch it, increment a
/// persistent retry counter in the job's data map, and schedule a new one-time trigger
/// at <c>now + <see cref="RetryAfterMinutes"/></c> — up to <see cref="MaxRetries"/> times.
/// The current execution always finishes cleanly so the scheduler does not mark it as failed.
/// <para>
/// Applicable to both <see cref="IScheduledJob"/> and <see cref="IScheduledJob{TParam}"/> implementations.
/// At runtime, retry behaviour can be overridden via <see cref="ScheduleConfig.EnableRetry"/>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [RetryConfig(MaxRetries = 5, RetryAfterMinutes = 10)]
/// public class MyJob : IScheduledJob { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RetryConfigAttribute : Attribute
{
    /// <summary>
    /// Maximum number of retry attempts after the initial failure.
    /// Default: <c>3</c>.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// How many minutes to wait before each retry attempt.
    /// Default: <c>5</c> minutes.
    /// </summary>
    public double RetryAfterMinutes { get; set; } = 5;
}

/// <summary>
/// Controls concurrency, crash-recovery, and misfire handling for a scheduled job.
/// When omitted, defaults apply: no concurrent execution, recovery enabled, misfire fires once.
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
    /// Whether the scheduler should attempt to re-execute this job after a crash or restart.
    /// Default: <c>true</c>.
    /// </summary>
    public bool RequestsRecovery { get; set; } = true;

    /// <summary>
    /// Behaviour when a trigger fires but the scheduled time has already passed.
    /// Default: <see cref="MisfireInstructions.FireOnce"/>.
    /// </summary>
    public MisfireInstructions MisfireInstructions { get; set; } = MisfireInstructions.FireOnce;
}
