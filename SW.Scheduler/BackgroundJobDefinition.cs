using System.Reflection;
using SW.PrimitiveTypes;

namespace SW.Scheduler;

internal static class JobKeyConventions
{
    /// <summary>
    /// The fixed Quartz JobKey.Name used for simple (non-parameterized) jobs.
    /// </summary>
    public const string MainJobName = "MAIN";

    /// <summary>
    /// Computes the Quartz JobKey.Group from a job type.
    /// Takes the last segment of the namespace + the class name.
    /// e.g. SampleApplication.Jobs.SendCustomerEmailsJob → "Jobs.SendCustomerEmailsJob"
    /// </summary>
    public static string GroupFromType(Type jobType)
    {
        var ns = jobType.Namespace ?? string.Empty;
        // Take only the last segment of the namespace
        var lastSegment = ns.Contains('.')
            ? ns[(ns.LastIndexOf('.') + 1)..]
            : ns;

        return string.IsNullOrEmpty(lastSegment)
            ? jobType.Name
            : $"{lastSegment}.{jobType.Name}";
    }
}

internal class ScheduledJobDefinition : IScheduledJobDefinition
{
    public string JobParamsTypeName { get; set; }
    public Type JobParamsType { get; set; }
    public MethodInfo ExecutMethod { get; set; }
    public Type JobType { get; set; }
    public bool WithParams => JobParamsType != null;
    public string Name => JobType.Name;

    /// <summary>
    /// Quartz JobKey.Group: last namespace segment + class name.
    /// e.g. "Jobs.SendCustomerEmailsJob"
    /// </summary>
    public string Group => JobKeyConventions.GroupFromType(JobType);
}
