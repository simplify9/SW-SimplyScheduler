namespace SW.Scheduler;

/// <summary>
/// Provides runtime scheduling and management of jobs.
/// Inject this interface to create, modify, pause, resume, or remove job schedules
/// without restarting the application.
/// </summary>
public interface IScheduleRepository
{
    // -------------------------------------------------------------------------
    // Schedule
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a recurring cron schedule for a parameterized job.
    /// Each <paramref name="scheduleKey"/> gets its own dedicated schedule, so
    /// <see cref="ScheduleConfig"/> (e.g. concurrency) is applied independently per schedule.
    /// </summary>
    /// <param name="param">The parameter object serialized into the job's data map.</param>
    /// <param name="cronExpression">A valid Quartz.NET cron expression.</param>
    /// <param name="scheduleKey">Unique key identifying this schedule. Used for all future management calls.</param>
    /// <param name="config">Optional runtime config overriding the job's <see cref="ScheduleConfigAttribute"/>.</param>
    Task Schedule<TScheduler, TParam>(TParam param, string cronExpression, string scheduleKey, ScheduleConfig? config = null)
        where TScheduler : IScheduledJob<TParam>;

    /// <summary>
    /// Creates or replaces the single default cron trigger for a simple job.
    /// Replaces any trigger set by <see cref="ScheduleAttribute"/> or a previous runtime call.
    /// Simple jobs support only one active cron trigger.
    /// </summary>
    /// <param name="cronExpression">A valid Quartz.NET cron expression.</param>
    /// <param name="config">Optional runtime config overriding the job's <see cref="ScheduleConfigAttribute"/>.</param>
    Task Schedule<TScheduler>(string cronExpression, ScheduleConfig? config = null)
        where TScheduler : IScheduledJob;

    // -------------------------------------------------------------------------
    // ScheduleOnce
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs a parameterized job once — immediately if <paramref name="runAt"/> is omitted,
    /// or at the specified UTC time. A unique schedule key is generated automatically and returned.
    /// </summary>
    /// <param name="param">The parameter object for this execution.</param>
    /// <param name="runAt">UTC time to run the job. Defaults to <see cref="DateTime.UtcNow"/>.</param>
    /// <param name="config">Optional runtime config overriding the job's <see cref="ScheduleConfigAttribute"/>.</param>
    /// <returns>The auto-generated schedule key that identifies this one-time job.</returns>
    Task<string> ScheduleOnce<TScheduler, TParam>(TParam param, DateTime? runAt = null, ScheduleConfig? config = null)
        where TScheduler : IScheduledJob<TParam>;

    // -------------------------------------------------------------------------
    // Reschedule
    // -------------------------------------------------------------------------

    /// <summary>
    /// Updates the cron expression for an existing parameterized schedule.
    /// </summary>
    /// <param name="scheduleKey">Key returned by or passed to <see cref="Schedule{TScheduler, TParam}"/>.</param>
    /// <param name="newCronExpression">The replacement cron expression.</param>
    Task RescheduleJob<TScheduler, TParam>(string scheduleKey, string newCronExpression)
        where TScheduler : IScheduledJob<TParam>;

    /// <summary>
    /// Updates the cron expression of the single default trigger for a simple job.
    /// The job must already be scheduled.
    /// </summary>
    /// <param name="newCronExpression">The replacement cron expression.</param>
    Task RescheduleJob<TScheduler>(string newCronExpression)
        where TScheduler : IScheduledJob;

    // -------------------------------------------------------------------------
    // Unschedule
    // -------------------------------------------------------------------------

    /// <summary>
    /// Permanently removes a parameterized schedule and its dedicated Quartz job.
    /// </summary>
    /// <param name="scheduleKey">Key returned by or passed to <see cref="Schedule{TScheduler, TParam}"/>.</param>
    Task UnscheduleJob<TScheduler, TParam>(string scheduleKey)
        where TScheduler : IScheduledJob<TParam>;

    /// <summary>
    /// Removes the default trigger from a simple job. The durable job registration is kept,
    /// allowing the job to be re-scheduled later.
    /// </summary>
    Task UnscheduleJob<TScheduler>()
        where TScheduler : IScheduledJob;

    // -------------------------------------------------------------------------
    // Pause / Resume
    // -------------------------------------------------------------------------

    /// <summary>Pauses all triggers associated with a parameterized schedule.</summary>
    /// <param name="scheduleKey">Key returned by or passed to <see cref="Schedule{TScheduler, TParam}"/>.</param>
    Task PauseJob<TScheduler, TParam>(string scheduleKey)
        where TScheduler : IScheduledJob<TParam>;

    /// <summary>Pauses the default trigger of a simple job.</summary>
    Task PauseJob<TScheduler>()
        where TScheduler : IScheduledJob;

    /// <summary>Resumes all triggers associated with a previously paused parameterized schedule.</summary>
    /// <param name="scheduleKey">Key returned by or passed to <see cref="Schedule{TScheduler, TParam}"/>.</param>
    Task ResumeJob<TScheduler, TParam>(string scheduleKey)
        where TScheduler : IScheduledJob<TParam>;

    /// <summary>Resumes the default trigger of a previously paused simple job.</summary>
    Task ResumeJob<TScheduler>()
        where TScheduler : IScheduledJob;

    // -------------------------------------------------------------------------
    // Discovery
    // -------------------------------------------------------------------------

    /// <summary>Returns metadata for all job types discovered and registered at startup.</summary>
    IEnumerable<IScheduledJobDefinition> GetJobDefinitions();
}

