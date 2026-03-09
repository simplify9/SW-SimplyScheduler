using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using SW.Scheduler.Monitoring;

namespace SW.Scheduler;

public class SchedulerPreparation(IServiceProvider serviceProvider, ILogger<SchedulerPreparation> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var jobsDiscovery = scope.ServiceProvider.GetRequiredService<JobsDiscovery>();
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        var schedulerOptions = scope.ServiceProvider.GetRequiredService<SchedulerOptions>();
        var scheduler = await schedulerFactory.GetScheduler(stoppingToken);

        // Register job listener for monitoring
        scheduler.ListenerManager.AddJobListener(
            scope.ServiceProvider.GetRequiredService<JobExecutionListener>());

        foreach (var jobDefinition in jobsDiscovery.All)
        {
            // Parameterized jobs are not registered as durable shared jobs —
            // each schedule creates its own dedicated Quartz job at runtime.
            if (jobDefinition.WithParams) continue;

            try
            {
                await RegisterJob(scheduler, jobDefinition, stoppingToken);
            }
            catch (Exception ex)
            {
                // Log and continue so one bad job doesn't prevent others from registering.
                logger.LogError(ex,
                    "Failed to register or schedule job {Group}. It will be skipped.",
                    jobDefinition.Group);
            }
        }

        // Register the cleanup job (only meaningful when a monitoring store is registered)
        await RegisterCleanupJob(scheduler, schedulerOptions, stoppingToken);
    }

    private async Task RegisterJob(IScheduler scheduler, ScheduledJobDefinition jobDefinition, CancellationToken stoppingToken)
    {
        var config = ScheduleConfigExtensions.FromAttribute(jobDefinition.JobType);

        // JobKey: Name = "MAIN", Group = "LastNs.ClassName"
        var jobKey = new JobKey(JobKeyConventions.MainJobName, jobDefinition.Group);

        var job = JobBuilder.Create<QuartzBackgroundJob>()
            .WithIdentity(jobKey)
            .StoreDurably()
            .ApplyConfig(config)
            .Build();

        var existingJob = await scheduler.GetJobDetail(jobKey, stoppingToken);
        if (existingJob == null)
        {
            await scheduler.AddJob(job, replace: true, stoppingToken);
            logger.LogInformation(
                "Registered durable job: {Group} (concurrent={Concurrent})",
                jobDefinition.Group, config.AllowConcurrentExecution);
        }
        else
        {
            // Config may have changed (e.g. AllowConcurrentExecution flipped) — replace the stored job.
            await scheduler.AddJob(job, replace: true, stoppingToken);
            logger.LogDebug(
                "Updated existing job definition: {Group} (concurrent={Concurrent})",
                jobDefinition.Group, config.AllowConcurrentExecution);
        }

        // Auto-schedule jobs with [Schedule] attribute.
        if (jobDefinition.JobType.GetCustomAttributes(typeof(ScheduleAttribute), false)
                .FirstOrDefault() is not ScheduleAttribute scheduleAttr)
            return;

        // Validate cron at startup so a misconfigured attribute fails fast with a clear message.
        try
        {
            scheduleAttr.CronExpression.ValidateCronExpression();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[Schedule] attribute on '{jobDefinition.JobType.Name}' has an invalid cron expression " +
                $"'{scheduleAttr.CronExpression}': {ex.Message}", ex);
        }

        var triggerKey = new TriggerKey(Constants.DefaultTriggerKey(jobDefinition.Group), jobDefinition.Group);

        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(scheduleAttr.CronExpression,
                b => b.ApplyMisfire(config.MisfireInstructions))
            .WithDescription(scheduleAttr.Description)
            .Build();

        var existingTrigger = await scheduler.GetTrigger(triggerKey, stoppingToken);
        if (existingTrigger == null)
        {
            await scheduler.ScheduleJob(newTrigger, stoppingToken);
            logger.LogInformation("Auto-scheduled {Group} with cron: {Cron}",
                jobDefinition.Group, scheduleAttr.CronExpression);
        }
        else
        {
            // Cron may have changed in a redeployment — always update the stored trigger.
            await scheduler.RescheduleJob(triggerKey, newTrigger, stoppingToken);
            logger.LogInformation("Updated schedule for {Group} with cron: {Cron}",
                jobDefinition.Group, scheduleAttr.CronExpression);
        }
    }

    private async Task RegisterCleanupJob(IScheduler scheduler, SchedulerOptions options, CancellationToken ct)
    {
        const string cleanupGroup = "SW.Scheduler.Internal";
        const string cleanupName  = "JobExecutionCleanup";

        var jobKey     = new JobKey(cleanupName, cleanupGroup);
        var triggerKey = new TriggerKey($"{cleanupName}_Trigger", cleanupGroup);

        var job = JobBuilder.Create<JobExecutionCleanupJob>()
            .WithIdentity(jobKey)
            .StoreDurably()
            .RequestRecovery(false)
            .Build();

        await scheduler.AddJob(job, replace: true, ct);

        // Validate the cron expression — fail fast with a clear error if misconfigured.
        if (!CronExpression.IsValidExpression(options.CleanupCronExpression))
        {
            logger.LogError(
                "[Cleanup] Invalid CleanupCronExpression '{Cron}'. The cleanup job will not be scheduled.",
                options.CleanupCronExpression);
            return;
        }

        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(options.CleanupCronExpression,
                b => b.WithMisfireHandlingInstructionDoNothing())
            .WithDescription($"Deletes JobExecution records older than {options.RetentionDays} days.")
            .Build();

        var existingTrigger = await scheduler.GetTrigger(triggerKey, ct);
        if (existingTrigger == null)
        {
            await scheduler.ScheduleJob(newTrigger, ct);
            logger.LogInformation("[Cleanup] Scheduled cleanup job with cron: {Cron}", options.CleanupCronExpression);
        }
        else
        {
            await scheduler.RescheduleJob(triggerKey, newTrigger, ct);
            logger.LogDebug("[Cleanup] Updated cleanup job cron to: {Cron}", options.CleanupCronExpression);
        }
    }
}