using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
        // Simple jobs have the type name derivable from the group ("LastNs.ClassName").
        string jobTypeName, jobGroup;

        var hasTypeName = context.JobDetail.JobDataMap.TryGetString(Constants.JobTypeNameKey, out var storedName)
                          && !string.IsNullOrWhiteSpace(storedName);
        var hasGroup    = context.JobDetail.JobDataMap.TryGetString(Constants.JobGroupKey,    out var storedGroup)
                          && !string.IsNullOrWhiteSpace(storedGroup);

        if (hasTypeName && hasGroup)
        {
            jobTypeName = storedName!;
            jobGroup    = storedGroup!;
        }
        else
        {
            // Simple job — derive type name from the last segment of the group.
            jobGroup = jobKey.Group;
            var dot = jobGroup.LastIndexOf('.');
            jobTypeName = dot >= 0 ? jobGroup[(dot + 1)..] : jobGroup;
        }

        var jobDefinition = jobsDiscovery.GetJobDefinition(jobTypeName, jobGroup);
        if (jobDefinition == null)
        {
            logger.LogError(
                "Job type '{TypeName}' in group '{Group}' not found in jobs discovery. Removing trigger.",
                jobTypeName, jobGroup);
            await context.Scheduler.UnscheduleJob(context.Trigger.Key);
            return;
        }

        // Resolve the job implementation from a fresh DI scope so scoped services
        // (e.g. DbContext) work correctly and are disposed after each execution.
        using var scope = serviceProvider.CreateScope();
        var svc = scope.ServiceProvider.GetService(jobDefinition.JobType);
        if (svc == null)
        {
            logger.LogError(
                "Service '{ServiceType}' could not be resolved. " +
                "Ensure the job is registered in the DI container via AddScheduler().",
                jobDefinition.JobType.FullName);
            return;
        }

        var execMethod = jobDefinition.ExecutMethod;

        if (jobDefinition.WithParams)
        {
            var exists = context.MergedJobDataMap.TryGetString(Constants.JobParamsKey, out var value);
            if (!exists || string.IsNullOrWhiteSpace(value))
            {
                logger.LogError(
                    "Job '{TypeName}' requires parameters but none were found in the job data map.",
                    jobTypeName);
                return;
            }

            var jobParams = JsonSerializer.Deserialize(value, jobDefinition.JobParamsType);
            if (jobParams is null)
            {
                logger.LogError(
                    "Deserialized params for job '{TypeName}' resolved to null. Raw value: {Value}",
                    jobTypeName, value);
                return;
            }

            var paramResult = execMethod.Invoke(svc, [jobParams]);
            if (paramResult is not Task paramTask)
                throw new InvalidOperationException(
                    $"Execute method on '{jobDefinition.JobType.FullName}' did not return a Task.");
            await paramTask;
        }
        else
        {
            var result = execMethod.Invoke(svc, null);
            if (result is not Task task)
                throw new InvalidOperationException(
                    $"Execute method on '{jobDefinition.JobType.FullName}' did not return a Task.");
            await task;
        }
    }
}

/// <summary>
/// Single Quartz IJob wrapper for all scheduled jobs.
/// Concurrency is controlled per job via JobBuilder.DisallowConcurrentExecution()
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
            throw new JobExecutionException(e, refireImmediately: false);
        }
    }
}