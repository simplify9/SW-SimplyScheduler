namespace SW.Scheduler.IntegrationTests.Jobs;

/// <summary>
/// Simple (non-parameterized) job used to verify basic cron scheduling,
/// pause/resume, and unschedule behaviour.
/// No [Schedule] attribute — controlled entirely via IScheduleRepository.
/// </summary>
[ScheduleConfig(MisfireInstructions = MisfireInstructions.Skip)]
public class SimpleTestJob : IScheduledJob
{
    public Task Execute()
    {
        TestJobSink.Fired.Enqueue(new JobFired(nameof(SimpleTestJob), null, DateTime.UtcNow));
        return Task.CompletedTask;
    }
}
