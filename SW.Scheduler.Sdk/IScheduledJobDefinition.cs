namespace SW.PrimitiveTypes;

/// <summary>
/// Exposes metadata about a discovered scheduled job, used by the scheduler at startup and runtime.
/// </summary>
public interface IScheduledJobDefinition
{
    /// <summary>The concrete CLR type that implements the job.</summary>
    Type JobType { get; }

    /// <summary>
    /// The CLR type of the job's parameter object, or <c>null</c> for simple (non-parameterized) jobs.
    /// </summary>
    Type JobParamsType { get; }

    /// <summary><c>true</c> if this job requires a parameter object to execute.</summary>
    bool WithParams { get; }

    /// <summary>The simple class name of the job type (e.g. <c>SendCustomerEmailsJob</c>).</summary>
    string Name { get; }

    /// <summary>
    /// The Quartz <c>JobKey</c> group, composed of the last namespace segment and the class name.
    /// <example>
    /// <c>SampleApplication.Jobs.SendCustomerEmailsJob</c> → <c>"Jobs.SendCustomerEmailsJob"</c>
    /// </example>
    /// </summary>
    string Group { get; }
}

