using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using SW.PrimitiveTypes;

namespace SW.Scheduler;

public class SchedulerPreparation(IServiceProvider serviceProvider, ILogger<SchedulerPreparation> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var jobsDiscovery = scope.ServiceProvider.GetRequiredService<JobsDiscovery>();
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler(stoppingToken);

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
}