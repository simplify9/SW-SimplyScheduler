using System.Collections.Concurrent;

namespace SW.Scheduler.IntegrationTests.Jobs;

/// <summary>
/// Thread-safe record that test jobs write to on every execution.
/// Tests poll this to verify jobs fire without needing real-time signals.
/// Call <see cref="Reset"/> at the start of each test method.
/// </summary>
public static class TestJobSink
{
    public static ConcurrentQueue<JobFired> Fired { get; } = new();

    public static void Reset()
    {
        while (Fired.TryDequeue(out _)) { }
    }

    public static int CountFor(string jobName)
        => Fired.Count(e => e.JobName == jobName);

    public static IReadOnlyList<JobFired> AllFor(string jobName)
        => Fired.Where(e => e.JobName == jobName).ToList();
}

/// <summary>One execution record written by a test job.</summary>
public sealed record JobFired(string JobName, JobParam? Params, DateTime At);
