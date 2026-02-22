using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using SW.PrimitiveTypes;

namespace SW.Scheduler;

public class SchedulerPreparation(IServiceProvider serviceProvider, ILogger<SchedulerPreparation> logger) : BackgroundService
{
    static JsonSerializerOptions _serializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var jobsDiscovery = scope.ServiceProvider.GetRequiredService<JobsDiscovery>();
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler(stoppingToken);


        foreach (var jobDefinition in jobsDiscovery.All)
        {
            // Register all jobs as durable jobs in Quartz
            var job = JobBuilder.Create<QuartzBackgroundJob>()
                .WithIdentity(jobDefinition.Name, jobDefinition.Namespace)
                .StoreDurably()
                .RequestRecovery()
                .Build();

            var existingJob = await scheduler.GetJobDetail(job.Key, stoppingToken);
            if (existingJob == null)
            {
                await scheduler.AddJob(job, true, stoppingToken);
                logger.LogInformation($"Registered durable job: {jobDefinition.Name} in namespace {jobDefinition.Namespace}");
            }
            
            // Auto-schedule jobs with [Schedule] attribute (only for IScheduledJob, not IScheduledJob<T>)
            if (jobDefinition.WithParams) continue;
            if (jobDefinition.JobType.GetCustomAttributes(typeof(ScheduleAttribute), false)
                    .FirstOrDefault() is not ScheduleAttribute scheduleAttr) continue;
            var triggerKey = scheduleAttr.TriggerKey ?? $"{jobDefinition.Name}_DefaultTrigger";
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey, jobDefinition.Namespace)
                .ForJob(job.Key)
                .WithCronSchedule(scheduleAttr.CronExpression)
                .WithDescription(scheduleAttr.Description)
                .Build();
                    
            // Only create if trigger doesn't exist
            var existingTrigger = await scheduler.GetTrigger(trigger.Key, stoppingToken);
            if (existingTrigger == null)
            {
                await scheduler.ScheduleJob(trigger, stoppingToken);
                logger.LogInformation($"Auto-scheduled job {jobDefinition.Name} with cron: {scheduleAttr.CronExpression}");
            }
            else
            {
                logger.LogInformation($"Trigger {triggerKey} already exists for job {jobDefinition.Name}, skipping auto-schedule");
            }
        }
    }
}