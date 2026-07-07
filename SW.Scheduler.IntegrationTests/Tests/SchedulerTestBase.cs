using Microsoft.Extensions.DependencyInjection;
using Quartz;
using SW.Scheduler.IntegrationTests.Infrastructure;
using SW.Scheduler.IntegrationTests.Jobs;
using Xunit;

namespace SW.Scheduler.IntegrationTests.Tests;

/// <summary>
/// Abstract base that contains all integration tests shared across all three database
/// providers. Concrete subclasses supply a started IHost with the appropriate
/// persistent Quartz store pre-created via EnsureCreated.
///
/// Each test:
///   1. Calls CreateHostAsync() → fresh Quartz schema + started IHost.
///   2. Resets TestJobSink.
///   3. Resolves IScheduleRepository and IScheduleReader from a DI scope.
///   4. Exercises the scheduler.
///   5. Disposes the HostHandle (stops Quartz, drops schema).
/// </summary>
public abstract class SchedulerTestBase
{
    protected SchedulerTestBase()
    {
        // Quartz 3.x keeps a static log provider that wraps the host's ILoggerFactory.
        // When a test host is disposed the factory is disposed too, but the static field
        // s_currentLogProvider still holds a reference to it.  On the next test, Quartz's
        // ContainerConfigurationProcessor crashes inside XMLSchedulingDataProcessor.<ctor>
        // because GetLogger() calls the disposed factory.
        //
        // Fix: null out s_currentLogProvider via reflection before each test so that Quartz
        // falls back to its no-op logger path until the new host wires up a fresh provider.
        var field = typeof(Quartz.Logging.LogProvider)
            .GetField("s_currentLogProvider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field?.SetValue(null, null);
    }

    // Every second — gives tests a reliable, fast-firing schedule.
    private const string EverySec = "* * * * * ?";

    // Far-future cron used when we need a job that will NOT fire spontaneously.
    private const string Never = "0 0 0 1 1 ? 2099";

    // ─────────────────────────────────────────────────────────────────────────
    // Provider-specific factory — implemented by each concrete subclass
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Build, seed (EnsureCreated), and start a fresh IHost configured with
    /// the concrete provider. Called once per test method.
    /// </summary>
    protected abstract Task<HostHandle> CreateHostAsync();

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Simple job fires on a cron schedule
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Schedule_SimpleJob_FiresOnCronSchedule()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        await repo.Schedule<SimpleTestJob>(EverySec);

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(SimpleTestJob)) >= 1,
            timeoutSeconds: 10,
            "SimpleTestJob to fire at least once");

        Assert.True(TestJobSink.CountFor(nameof(SimpleTestJob)) >= 1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. ScheduleOnce fires immediately with correct params
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleOnce_ParamJob_FiresImmediately()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        await repo.ScheduleOnce<ParamTestJob, JobParam>(new JobParam(99, "once"));

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(ParamTestJob)) >= 1,
            timeoutSeconds: 10,
            "ParamTestJob one-shot to fire");

        // Should fire exactly once and not repeat
        await Task.Delay(2_000);
        Assert.Equal(1, TestJobSink.CountFor(nameof(ParamTestJob)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Parameterized job delivers correct params to Execute
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Schedule_ParamJob_DeliversCorrectParams()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        await repo.Schedule<ParamTestJob, JobParam>(
            new JobParam(42, "hello"),
            EverySec,
            scheduleKey: "param-delivery-key");

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(ParamTestJob)) >= 1,
            timeoutSeconds: 10,
            "ParamTestJob with params to fire");

        var fired = TestJobSink.AllFor(nameof(ParamTestJob)).First();
        Assert.NotNull(fired.Params);
        Assert.Equal(42, fired.Params.Id);
        Assert.Equal("hello", fired.Params.Label);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. ScheduleIfNotExists returns true first time, false on duplicate
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleIfNotExists_ReturnsTrueOnCreate_FalseOnDuplicate()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        bool first  = await repo.ScheduleIfNotExists<ParamTestJob, JobParam>(
            new JobParam(1, "seed"), EverySec, "idem-key");
        bool second = await repo.ScheduleIfNotExists<ParamTestJob, JobParam>(
            new JobParam(1, "seed"), EverySec, "idem-key");

        Assert.True(first,   "first call must return true (newly created)");
        Assert.False(second, "second call must return false (already exists)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. ScheduleIfNotExists registers the job only once (no duplicate firing)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleIfNotExists_SecondCallDoesNotDuplicateJob()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        // Two calls with the same key — first wins
        await repo.ScheduleIfNotExists<ParamTestJob, JobParam>(
            new JobParam(10, "a"), EverySec, "no-dup-key");
        await repo.ScheduleIfNotExists<ParamTestJob, JobParam>(
            new JobParam(10, "a"), EverySec, "no-dup-key");

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(ParamTestJob)) >= 3,
            timeoutSeconds: 10,
            "at least 3 firings to confirm cron is running");

        // If the job was duplicated there would be 2× the firing rate; the
        // results should all have the same params (from the first registration).
        var allFired = TestJobSink.AllFor(nameof(ParamTestJob));
        Assert.All(allFired, f => Assert.Equal(10, f.Params!.Id));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. PauseJob stops the job from firing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PauseJob_StopsFiring()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        await repo.Schedule<SimpleTestJob>(EverySec);

        // Wait for at least one fire to confirm the job is running
        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(SimpleTestJob)) >= 1,
            timeoutSeconds: 10,
            "first SimpleTestJob fire");

        await repo.PauseJob<SimpleTestJob>();

        int countAfterPause = TestJobSink.CountFor(nameof(SimpleTestJob));
        await Task.Delay(3_000); // wait 3 seconds — cron would fire 3× if not paused
        int countAfterWait  = TestJobSink.CountFor(nameof(SimpleTestJob));

        Assert.Equal(countAfterPause, countAfterWait);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. ResumeJob restores firing after a pause
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResumeJob_RestoresFiring()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        await repo.Schedule<SimpleTestJob>(EverySec);

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(SimpleTestJob)) >= 1,
            timeoutSeconds: 10,
            "first SimpleTestJob fire before pause");

        await repo.PauseJob<SimpleTestJob>();
        await Task.Delay(2_000);

        int countBeforeResume = TestJobSink.CountFor(nameof(SimpleTestJob));

        await repo.ResumeJob<SimpleTestJob>();

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(SimpleTestJob)) > countBeforeResume,
            timeoutSeconds: 10,
            "SimpleTestJob to fire after resume");

        Assert.True(TestJobSink.CountFor(nameof(SimpleTestJob)) > countBeforeResume);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. UnscheduleJob permanently stops the job
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnscheduleJob_PermanentlyStopsFiring()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        await repo.Schedule<SimpleTestJob>(EverySec);

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(SimpleTestJob)) >= 1,
            timeoutSeconds: 10,
            "first SimpleTestJob fire");

        await repo.UnscheduleJob<SimpleTestJob>();

        int countAfterUnschedule = TestJobSink.CountFor(nameof(SimpleTestJob));
        await Task.Delay(3_000);
        int countAfterWait       = TestJobSink.CountFor(nameof(SimpleTestJob));

        Assert.Equal(countAfterUnschedule, countAfterWait);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. RescheduleJob replaces the cron expression
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RescheduleJob_UpdatesCronExpression()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        // Start with a cron that will never fire in practice
        await repo.Schedule<SimpleTestJob>(Never);

        await Task.Delay(500); // confirm no spontaneous fires
        Assert.Equal(0, TestJobSink.CountFor(nameof(SimpleTestJob)));

        // Switch to every-second
        await repo.RescheduleJob<SimpleTestJob>(EverySec);

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(SimpleTestJob)) >= 1,
            timeoutSeconds: 10,
            "SimpleTestJob to fire after reschedule");

        Assert.True(TestJobSink.CountFor(nameof(SimpleTestJob)) >= 1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Multiple parameterized schedules run independently
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleParamSchedules_RunIndependently()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        await repo.Schedule<ParamTestJob, JobParam>(new JobParam(1, "alpha"), EverySec, "multi-key-1");
        await repo.Schedule<ParamTestJob, JobParam>(new JobParam(2, "beta"),  EverySec, "multi-key-2");

        await TestWaiter.WaitUntilAsync(
            () =>
                TestJobSink.AllFor(nameof(ParamTestJob)).Any(f => f.Params?.Id == 1) &&
                TestJobSink.AllFor(nameof(ParamTestJob)).Any(f => f.Params?.Id == 2),
            timeoutSeconds: 10,
            "both param schedules to fire");

        Assert.Contains(TestJobSink.AllFor(nameof(ParamTestJob)), f => f.Params?.Label == "alpha");
        Assert.Contains(TestJobSink.AllFor(nameof(ParamTestJob)), f => f.Params?.Label == "beta");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Successful executions are recorded in the monitoring store
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JobExecution_SuccessRecordedInMonitoring()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo   = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        var reader = scope.ServiceProvider.GetRequiredService<IScheduleReader>();

        await repo.Schedule<ParamTestJob, JobParam>(
            new JobParam(7, "monitoring"), EverySec, "mon-success-key");

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(ParamTestJob)) >= 1,
            timeoutSeconds: 10,
            "ParamTestJob to fire for monitoring test");

        // Small buffer: the monitoring record write is async after job completion
        await Task.Delay(500);

        var last = await reader.GetLastExecution<ParamTestJob, JobParam>("mon-success-key");

        Assert.NotNull(last);
        Assert.Equal(true, last.Success);
        Assert.Null(last.Error);
        Assert.NotNull(last.EndTimeUtc);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. Failed executions are recorded with error details
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JobExecution_FailureRecordedInMonitoring()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo   = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        var reader = scope.ServiceProvider.GetRequiredService<IScheduleReader>();

        // Schedule without retry so it fails cleanly
        await repo.Schedule<FailingJob, JobParam>(
            new JobParam(8, "fail"), EverySec, "mon-fail-key");

        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(FailingJob)) >= 1,
            timeoutSeconds: 10,
            "FailingJob to fire");

        await Task.Delay(500);

        var failures = await reader.GetFailedExecutions<FailingJob, JobParam>(
            "mon-fail-key", since: DateTime.UtcNow.AddMinutes(-5));

        Assert.NotEmpty(failures);
        var failure = failures.First();
        Assert.Equal(false, failure.Success);
        Assert.NotNull(failure.Error);
        Assert.Contains("Intentional failure", failure.Error);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13. Running executions are visible in the monitoring store mid-flight
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRunningExecutions_ShowsInFlightJob()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo   = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        var reader = scope.ServiceProvider.GetRequiredService<IScheduleReader>();

        // SlowTestJob writes to sink immediately then sleeps 3 seconds
        await repo.ScheduleOnce<SlowTestJob, JobParam>(new JobParam(0, "slow"));

        // Wait until the job has started (sink entry appears) then query
        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(SlowTestJob)) >= 1,
            timeoutSeconds: 10,
            "SlowTestJob to start");

        // Brief pause to let the monitoring store commit the StartTimeUtc record
        await Task.Delay(300);

        var running = await reader.GetRunningExecutions();

        Assert.Contains(running, e => e.JobGroup.Contains(nameof(SlowTestJob)));

        // Let it finish (3 s sleep + buffer)
        await Task.Delay(4_000);

        var runningAfter = await reader.GetRunningExecutions();
        Assert.DoesNotContain(runningAfter, e => e.JobGroup.Contains(nameof(SlowTestJob)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14. Retry config causes re-execution after failure
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryConfig_JobRetriesAfterFailure()
    {
        await using var host = await CreateHostAsync();
        TestJobSink.Reset();
        using var scope = host.Services.CreateScope();
        var repo   = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        var reader = scope.ServiceProvider.GetRequiredService<IScheduleReader>();

        // ScheduleOnce with retry enabled: 2 retries, ~1-second intervals
        var key = await repo.ScheduleOnce<FailingJob, JobParam>(
            new JobParam(5, "retry"),
            config: new ScheduleConfig
            {
                EnableRetry      = true,
                MaxRetries       = 2,
                RetryAfterMinutes = 0.017  // ≈ 1 second
            });

        // Expect 3 total attempts: initial + 2 retries
        await TestWaiter.WaitUntilAsync(
            () => TestJobSink.CountFor(nameof(FailingJob)) >= 3,
            timeoutSeconds: 15,
            "3 total FailingJob attempts (initial + 2 retries)");

        await Task.Delay(500); // let monitoring records commit

        var failures = await reader.GetFailedExecutions<FailingJob, JobParam>(
            key, since: DateTime.UtcNow.AddMinutes(-5));

        // All 3 attempts should be recorded as failures
        Assert.Equal(3, failures.Count);
        Assert.All(failures, f =>
        {
            Assert.Equal(false, f.Success);
            Assert.NotNull(f.Error);
        });

        // No more retries after MaxRetries — verify count stays at 3
        await Task.Delay(2_000);
        Assert.Equal(3, TestJobSink.CountFor(nameof(FailingJob)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 15. GetJobDefinitions includes all registered job types
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetJobDefinitions_IncludesAllRegisteredJobs()
    {
        await using var host = await CreateHostAsync();
        using var scope = host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();

        var defs = repo.GetJobDefinitions().ToList();

        var names = defs.Select(d => d.Name).ToHashSet();

        Assert.Contains(nameof(SimpleTestJob), names);
        Assert.Contains(nameof(ParamTestJob),  names);
        Assert.Contains(nameof(SlowTestJob),   names);
        Assert.Contains(nameof(FailingJob),    names);

        // Parameterized jobs should expose their param type
        var paramDef = defs.Single(d => d.Name == nameof(ParamTestJob));
        Assert.True(paramDef.WithParams);
        Assert.Equal(typeof(JobParam), paramDef.JobParamsType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 16. Clustering is always on: the scheduler gets a unique instance identity,
    //     not Quartz's shared "NON_CLUSTERED" default (which breaks multi-node
    //     cluster coordination).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clustering_AssignsUniqueInstanceId_NotSharedDefault()
    {
        await using var host = await CreateHostAsync();
        using var scope = host.Services.CreateScope();
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();

        Assert.NotEqual("NON_CLUSTERED", scheduler.SchedulerInstanceId);
    }
}
