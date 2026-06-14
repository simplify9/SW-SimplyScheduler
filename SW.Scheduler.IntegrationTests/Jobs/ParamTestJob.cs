namespace SW.Scheduler.IntegrationTests.Jobs;

/// <summary>
/// Parameterized job that records its received params in the sink.
/// Used to verify param delivery, multiple independent schedules, and monitoring.
/// </summary>
[ScheduleConfig(MisfireInstructions = MisfireInstructions.Skip)]
public class ParamTestJob : IScheduledJob<JobParam>
{
    public Task Execute(JobParam jobParams)
    {
        TestJobSink.Fired.Enqueue(new JobFired(nameof(ParamTestJob), jobParams, DateTime.UtcNow));
        return Task.CompletedTask;
    }
}
