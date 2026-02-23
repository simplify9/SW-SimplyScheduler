using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quartz;

namespace SW.Scheduler;

// Shared execution logic extracted so both Quartz job types can reuse it without duplication.
internal static class QuartzJobExecutor
{
    public static async Task Execute(
        IJobExecutionContext context,
        IServiceProvider serviceProvider,
        JobsDiscovery jobsDiscovery,
        ILogger logger)
    {
        var jobKey = context.JobDetail.Key;

        // Dedicated parameterized jobs store both the original type name and group in JobDataMap.
        // Simple jobs use the type name directly resolvable from the group (which IS "LastNs.ClassName").
        // For simple jobs, the type name is the last segment of the group (after the last dot).
        string jobTypeName, jobGroup;

        var hasTypeName = context.JobDetail.JobDataMap.TryGetString(Constants.JobTypeNameKey, out var storedName)
                          && !string.IsNullOrWhiteSpace(storedName);
        var hasGroup    = context.JobDetail.JobDataMap.TryGetString(Constants.JobGroupKey,    out var storedGroup)
                          && !string.IsNullOrWhiteSpace(storedGroup);

        if (hasTypeName && hasGroup)
        {
            // Parameterized dedicated job
            jobTypeName = storedName!;
            jobGroup    = storedGroup!;
        }
        else
        {
            // Simple job — JobKey.Group IS the group, type name is the last part of the group
            jobGroup = jobKey.Group;
            var dot = jobGroup.LastIndexOf('.');
            jobTypeName = dot >= 0 ? jobGroup[(dot + 1)..] : jobGroup;
        }

        var jobDefinition = jobsDiscovery.GetJobDefinition(jobTypeName, jobGroup);
        if (jobDefinition == null)
        {
            logger.LogError("Job type '{TypeName}' in group '{Group}' not found in jobs discovery", jobTypeName, jobGroup);
            await context.Scheduler.UnscheduleJob(context.Trigger.Key);
            return;
        }

        var svc = serviceProvider.GetService(jobDefinition.JobType);
        if (svc == null)
        {
            logger.LogError("Service {ServiceType} not found in service provider", jobDefinition.JobType.FullName);
            return;
        }

        var execMethod = jobDefinition.ExecutMethod;

        if (jobDefinition.WithParams)
        {
            var exists = context.MergedJobDataMap.TryGetString(Constants.JobParamsKey, out var value);
            if (!exists || string.IsNullOrWhiteSpace(value))
            {
                logger.LogError("Job '{TypeName}' requires parameters but none found in job data map", jobTypeName);
                return;
            }

            var jobParams = JsonSerializer.Deserialize(value, jobDefinition.JobParamsType);
            await (Task)execMethod.Invoke(svc, new[] { jobParams });
        }
        else
        {
            await (Task)execMethod.Invoke(svc, null);
        }
    }
}

/// <summary>
/// Single Quartz IJob wrapper for all scheduled jobs.
/// Concurrency is controlled at the job-data level via JobBuilder.DisallowConcurrentExecution()
/// rather than a class-level attribute, so a single class handles both modes.
/// </summary>
[PersistJobDataAfterExecution]
internal class QuartzBackgroundJob(
    IServiceProvider serviceProvider,
    JobsDiscovery jobsDiscovery,
    ILogger<QuartzBackgroundJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await QuartzJobExecutor.Execute(context, serviceProvider, jobsDiscovery, logger);
        }
        catch (Exception e)
        {
            throw new JobExecutionException(e, false);
        }
    }
}