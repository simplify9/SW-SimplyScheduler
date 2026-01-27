using System.Reflection;
using SW.PrimitiveTypes;

namespace SW.Scheduler;

internal class ScheduledJobDefinition:IScheduledJobDefinition
{
    public string JobParamsTypeName { get; set; }
    public Type JobParamsType { get; set; }
    public MethodInfo ExecutMethod { get; set; }
    public Type JobType { get; set; }
    public bool WithParams => JobParamsType != null;
    
}

public class BackgroundJobOptions
{
    public int? RetryCount { get; set; }
    public uint? RetryAfterSeconds { get; set; }
    public int? Priority { get; set; }
}