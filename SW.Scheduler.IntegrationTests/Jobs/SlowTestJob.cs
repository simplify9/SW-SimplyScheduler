namespace SW.Scheduler.IntegrationTests.Jobs;

/// <summary>
/// Parameterized job that sleeps for 3 seconds before completing.
/// Used to verify GetRunningExecutions() and DisallowConcurrentExecution.
/// Writes to sink BEFORE sleeping so tests can detect when the job has started.
/// </summary>
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.Skip)]
public class SlowTestJob : IScheduledJob<JobParam>
{
    public async Task Execute(JobParam jobParams)
    {
        // Record to sink immediately so the test knows the job has started.
        // EndTimeUtc will not be set in the DB until after the 3-second sleep.
        TestJobSink.Fired.Enqueue(new JobFired(nameof(SlowTestJob), jobParams, DateTime.UtcNow));
        await Task.Delay(3_000);
    }
}
