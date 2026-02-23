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

            // Read [ScheduleConfig] attribute if present, otherwise use defaults.
            var config = ScheduleConfigExtensions.FromAttribute(jobDefinition.JobType);

            // JobKey: Name = "MAIN", Group = "LastNs.ClassName"  e.g. "Jobs.SendCustomerEmailsJob"
            var job = JobBuilder.Create<QuartzBackgroundJob>()
                .WithIdentity(JobKeyConventions.MainJobName, jobDefinition.Group)
                .StoreDurably()
                .ApplyConfig(config)
                .Build();

            var existingJob = await scheduler.GetJobDetail(job.Key, stoppingToken);
            if (existingJob == null)
            {
                await scheduler.AddJob(job, true, stoppingToken);
                logger.LogInformation(
                    "Registered durable job: {Group} (concurrent={Concurrent})",
                    jobDefinition.Group, config.AllowConcurrentExecution);
            }

            // Auto-schedule jobs with [Schedule] attribute.
            if (jobDefinition.JobType.GetCustomAttributes(typeof(ScheduleAttribute), false)
                    .FirstOrDefault() is not ScheduleAttribute scheduleAttr) continue;

            var triggerKey = Constants.DefaultTriggerKey(jobDefinition.Group);
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey, jobDefinition.Group)
                .ForJob(job.Key)
                .WithCronSchedule(scheduleAttr.CronExpression,
                    b => b.ApplyMisfire(config.MisfireInstructions))
                .WithDescription(scheduleAttr.Description)
                .Build();

            var existingTrigger = await scheduler.GetTrigger(trigger.Key, stoppingToken);
            if (existingTrigger == null)
            {
                await scheduler.ScheduleJob(trigger, stoppingToken);
                logger.LogInformation("Auto-scheduled {Group} with cron: {Cron}",
                    jobDefinition.Group, scheduleAttr.CronExpression);
            }
            else
            {
                logger.LogInformation("Trigger for {Group} already exists, skipping auto-schedule",
                    jobDefinition.Group);
            }
        }
    }
}