namespace SW.Scheduler.IntegrationTests.Jobs;

/// <summary>
/// Parameterized job that always throws. Used to verify:
/// - Failure records in monitoring (JobExecution.Success = false, Error != null)
/// - Self-rescheduling retry when ScheduleConfig.EnableRetry = true
/// No [RetryConfig] attribute — retry is opted into at scheduling time per test.
/// </summary>
[ScheduleConfig(MisfireInstructions = MisfireInstructions.Skip)]
public class FailingJob : IScheduledJob<JobParam>
{
    public Task Execute(JobParam jobParams)
    {
        // Record the attempt so tests can count total executions including retries.
        TestJobSink.Fired.Enqueue(new JobFired(nameof(FailingJob), jobParams, DateTime.UtcNow));
        throw new InvalidOperationException(
            $"Intentional failure for test (Id={jobParams.Id}, Label={jobParams.Label})");
    }
}
