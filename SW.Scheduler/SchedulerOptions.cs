using SW.Scheduler;

namespace SW.Scheduler;

public class SchedulerOptions
{
    public ushort DefaultRetryCount { get; set; }
    public uint DefaultRetryAfter { get; set; }

    public IDictionary<string, BackgroundJobOptions> Options { get; set; } =
        new Dictionary<string, BackgroundJobOptions>(StringComparer.OrdinalIgnoreCase);
}