namespace SW.PrimitiveTypes;

/// <summary>
/// Defines a scheduled job that can be discovered and executed by the scheduler
/// </summary>
public interface IScheduledJobDefinition
{
    Type JobType { get; }
    Type JobParamsType { get; }
    bool WithParams { get; }
    string Name { get; }
    string Namespace { get; }
} 

/// <summary>
/// Declarative schedule attribute for IScheduledJob implementations.
/// Use this to automatically schedule a job on startup.
/// Can be overridden at runtime using IScheduleRepository.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ScheduleAttribute : Attribute
{
    /// <summary>
    /// Cron expression defining when the job should run
    /// Example: "0 0 * * * ?" = Daily at midnight
    /// </summary>
    public string CronExpression { get; }
    
    /// <summary>
    /// Optional trigger key. If not specified, uses job type name
    /// </summary>
    public string? TriggerKey { get; set; }
    
    /// <summary>
    /// Optional description of the schedule
    /// </summary>
    public string? Description { get; set; }
    
    public ScheduleAttribute(string cronExpression)
    {
        CronExpression = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));
    }
}

public enum ScheduleType
{
    Every
}

public interface IScheduledJobBase
{
}

public interface IScheduledJobWithParams : IScheduledJobBase
{
}

/// <summary>
/// Simple scheduled job without parameters.
/// Use [Schedule] attribute for declarative scheduling or IScheduleRepository for runtime scheduling.
/// </summary>
public interface IScheduledJob : IScheduledJobBase
{
    Task Execute();
}

/// <summary>
/// Parameterized scheduled job.
/// Can only be scheduled at runtime via IScheduleRepository with specific parameter values.
/// </summary>
/// <typeparam name="TParam">Type of parameter object (must be JSON-serializable)</typeparam>
public interface IScheduledJob<TParam> : IScheduledJobWithParams
{
    Task Execute(TParam jobParams);
}

public interface ISchedule
{
    ScheduleType ScheduleType { get; }
}

/// <summary>
/// Repository for scheduling and managing jobs at runtime
/// </summary>
public interface IScheduleRepository
{
    /// <summary>
    /// Schedule a parameterized job with a cron expression at runtime.
    /// </summary>
    Task<string> Schedule<TScheduler, TParam>(TParam param, string cronExpression, string triggerKey) 
        where TScheduler : IScheduledJob<TParam>;
    
    /// <summary>
    /// Schedule a simple job with a cron expression at runtime.
    /// This will override any [Schedule] attribute on the job.
    /// </summary>
    Task<string> Schedule<TScheduler>(string cronExpression, string triggerKey) 
        where TScheduler : IScheduledJob;
    
    /// <summary>
    /// Schedule a parameterized job to run once. If runAt is not provided, runs immediately.
    /// A unique trigger key is generated automatically.
    /// </summary>
    Task<string> ScheduleOnce<TScheduler, TParam>(TParam param, DateTime? runAt = null) 
        where TScheduler : IScheduledJob<TParam>;
    
    /// <summary>
    /// Schedule a simple job to run once. If runAt is not provided, runs immediately.
    /// A unique trigger key is generated automatically.
    /// </summary>
    Task<string> ScheduleOnce<TScheduler>(DateTime? runAt = null) 
        where TScheduler : IScheduledJob;
    
    /// <summary>
    /// Reschedule an existing trigger with a new cron expression
    /// </summary>
    Task RescheduleJob(string triggerKey, string newCronExpression);
    
    /// <summary>
    /// Unschedule a job by removing its trigger
    /// </summary>
    Task UnscheduleJob(string triggerKey);
    
    /// <summary>
    /// Pause a scheduled job
    /// </summary>
    Task PauseJob(string triggerKey);
    
    /// <summary>
    /// Resume a paused job
    /// </summary>
    Task ResumeJob(string triggerKey);
    
    /// <summary>
    /// Get all registered job definitions
    /// </summary>
    IEnumerable<IScheduledJobDefinition> GetJobDefinitions();
}
