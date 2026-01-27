using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using SW.PrimitiveTypes;
using SW.Scheduler;

namespace SW.Scheduler;

public class SchedulerPreparation(IServiceProvider serviceProvider) : BackgroundService
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
            var jobInstance = scope.ServiceProvider.GetRequiredService(jobDefinition.JobType);
            object jobParams = null;
            if (jobDefinition.WithParams)
            {
                jobParams = Activator.CreateInstance(jobDefinition.JobParamsType);
            }

            var job = JobBuilder.Create<QuartzBackgroundJob>()
                .WithIdentity(jobDefinition.JobType)
                .StoreDurably()
                .RequestRecovery()
                .Build();

            if (jobParams != null)
                job.JobDataMap.Put("params",
                    JsonSerializer.Serialize(jobParams, jobDefinition.JobParamsType, _serializerOptions));

            var jobInstanceAsBase = jobInstance as IScheduledJobBase;
            if (jobInstanceAsBase == null)
                throw new InvalidOperationException("Job instance must implement IScheduledJobBase");


            var existingJob = await scheduler.GetJobDetail(job.Key, stoppingToken);
            if (existingJob == null)
                await scheduler.AddJob(job, true, stoppingToken);
            
        }
    }
}