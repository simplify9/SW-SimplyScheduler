namespace SW.Scheduler;

/// <summary>Marker base interface for all scheduled job types.</summary>
public interface IScheduledJobBase
{
}

/// <summary>Marker interface for parameterized scheduled jobs.</summary>
public interface IScheduledJobWithParams : IScheduledJobBase
{
}

/// <summary>
/// Simple scheduled job without parameters.
/// Use <see cref="ScheduleAttribute"/> for declarative scheduling,
/// or <see cref="IScheduleRepository"/> for runtime scheduling.
/// </summary>
public interface IScheduledJob : IScheduledJobBase
{
    Task Execute();
}

/// <summary>
/// Parameterized scheduled job.
/// Must be scheduled at runtime via <see cref="IScheduleRepository"/> with specific parameter values.
/// </summary>
/// <typeparam name="TParam">Type of the parameter object. Must be JSON-serializable.</typeparam>
public interface IScheduledJob<TParam> : IScheduledJobWithParams
{
    Task Execute(TParam jobParams);
}

