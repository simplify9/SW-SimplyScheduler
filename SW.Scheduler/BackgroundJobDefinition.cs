using System.Reflection;

namespace SW.Scheduler;

internal static class JobKeyConventions
{
    /// <summary>
    /// The fixed Quartz JobKey.Name used for simple (non-parameterized) jobs.
    /// </summary>
    public const string MainJobName = "MAIN";

    /// <summary>
    /// Computes the Quartz JobKey.Group from a job type.
    /// Takes the last two segments of the namespace + the class name to ensure
    /// uniqueness across modules while remaining safely within the 200-character
    /// database column limit imposed by the Quartz schema on all supported providers
    /// (PostgreSQL, SQL Server, MySQL).
    /// e.g. SampleApplication.Jobs.SendCustomerEmailsJob → "Jobs.SendCustomerEmailsJob"
    ///      Company.Product.Module.Jobs.SendCustomerEmailsJob → "Module.Jobs.SendCustomerEmailsJob"
    /// </summary>
    public static string GroupFromType(Type jobType)
    {
        var ns = jobType.Namespace ?? string.Empty;
        var parts = ns.Split('.', StringSplitOptions.RemoveEmptyEntries);

        var prefix = parts.Length switch
        {
            0 => string.Empty,
            1 => parts[0],
            _ => string.Join('.', parts[^2], parts[^1])
        };

        return string.IsNullOrEmpty(prefix)
            ? jobType.Name
            : $"{prefix}.{jobType.Name}";
    }
}

internal class ScheduledJobDefinition : IScheduledJobDefinition
{
    public string? JobParamsTypeName { get; set; }
    public Type? JobParamsType { get; set; }
    public required MethodInfo ExecutMethod { get; set; }
    public required Type JobType { get; set; }
    public bool WithParams => JobParamsType != null;
    public string Name => JobType.Name;

    /// <summary>
    /// Quartz JobKey.Group: last two namespace segments + class name.
    /// e.g. "Module.Jobs.SendCustomerEmailsJob"
    /// </summary>
    public string Group => JobKeyConventions.GroupFromType(JobType);
}
