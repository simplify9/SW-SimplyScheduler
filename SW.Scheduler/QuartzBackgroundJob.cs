using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace SW.Scheduler;

internal static class QuartzJobExecutor
{
    public static async Task Execute(
        IJobExecutionContext context,
        IServiceProvider serviceProvider,
        JobsDiscovery jobsDiscovery,
        ILogger logger)
    {
        var jobKey = context.JobDetail.Key;

        // ── Resolve job definition ────────────────────────────────────────────
        string jobTypeName, jobGroup;

        var hasTypeName = context.JobDetail.JobDataMap.TryGetString(Constants.JobTypeNameKey, out var storedName)
                          && !string.IsNullOrWhiteSpace(storedName);
        var hasGroup = context.JobDetail.JobDataMap.TryGetString(Constants.JobGroupKey, out var storedGroup)
                       && !string.IsNullOrWhiteSpace(storedGroup);

        if (hasTypeName && hasGroup)
        {
            jobTypeName = storedName!;
            jobGroup = storedGroup!;
        }
        else
        {
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

        // ── Resolve service from a fresh scope ───────────────────────────────
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

        // ── Read retry config from JobDataMap (written by ApplyConfig) ───────
        var dataMap = context.JobDetail.JobDataMap;
        var retryMax = dataMap.ContainsKey(Constants.RetryMaxKey)
            ? Convert.ToInt32(dataMap[Constants.RetryMaxKey])
            : (int?)null;
        var retryAfterMinutes = dataMap.ContainsKey(Constants.RetryAfterMinutesKey)
            ? Convert.ToDouble(dataMap[Constants.RetryAfterMinutesKey])
            : 5.0;
        var retryEnabled = retryMax.HasValue && retryMax.Value > 0;

        // ── Execute the job ──────────────────────────────────────────────────
        try
        {
            await InvokeExecute(context, jobDefinition, svc, jobTypeName);

            // Successful execution — clear any previous retry state.
            if (retryEnabled)
            {
                context.JobDetail.JobDataMap.Remove(Constants.RetryCountKey);
                context.JobDetail.JobDataMap.Remove(Constants.LastErrorKey);
            }
        }
        catch (Exception ex) when (retryEnabled)
        {
            await HandleRetry(context, jobDefinition, ex, retryMax!.Value, retryAfterMinutes, logger);
        }
        // If retry is not configured, the exception propagates to QuartzBackgroundJob
        // which wraps it in JobExecutionException (normal Quartz error path).
    }

    // ── Self-rescheduling retry logic ────────────────────────────────────────

    private static async Task HandleRetry(
        IJobExecutionContext context,
        ScheduledJobDefinition jobDefinition,
        Exception ex,
        int maxRetries,
        double retryAfterMinutes,
        ILogger logger)
    {
        var dataMap = context.JobDetail.JobDataMap;

        // Read and increment persisted retry counter.
        var currentRetry = dataMap.TryGetValue(Constants.RetryCountKey, out var retryValue)
            ? Convert.ToInt32(retryValue)
            : 0;
        currentRetry++;

        // Persist the updated state into the data map (PersistJobDataAfterExecution ensures this is saved).
        dataMap[Constants.RetryCountKey] = currentRetry;
        dataMap[Constants.LastErrorKey] = $"[Attempt {currentRetry}] {ex.GetType().Name}: {ex.Message}";

        if (currentRetry <= maxRetries)
        {
            var runAt = DateTimeOffset.UtcNow.AddMinutes(retryAfterMinutes);
            var baseKey = context.JobDetail.Key.Name;
            var retryTriggerKey = new TriggerKey(
                Constants.RetryTriggerKey(baseKey, currentRetry),
                context.JobDetail.Key.Group);

            // Clone the job data map so the retry trigger carries all params + updated retry state.
            var retryTrigger = TriggerBuilder.Create()
                .WithIdentity(retryTriggerKey)
                .ForJob(context.JobDetail.Key)
                .StartAt(runAt)
                .UsingJobData(dataMap)
                .Build();

            await context.Scheduler.ScheduleJob(retryTrigger);

            logger.LogWarning(
                "Job '{TypeName}' failed (attempt {Attempt}/{Max}). " +
                "Retry scheduled at {RunAt:O}. Error: {Error}",
                jobDefinition.Name, currentRetry, maxRetries, runAt, ex.Message);
        }
        else
        {
            logger.LogError(ex,
                "Job '{TypeName}' failed after {Max} attempt(s). No further retries will be scheduled. " +
                "Last error saved to job data map under key '{Key}'.",
                jobDefinition.Name, maxRetries, Constants.LastErrorKey);
        }
        // Return normally — Quartz will not mark this execution as failed.
    }

    // ── Invoke the user's Execute method ────────────────────────────────────

    private static async Task InvokeExecute(
        IJobExecutionContext context,
        ScheduledJobDefinition jobDefinition,
        object svc,
        string jobTypeName)
    {
        var execMethod = jobDefinition.ExecutMethod;

        if (jobDefinition.WithParams)
        {
            context.MergedJobDataMap.TryGetString(Constants.JobParamsKey, out var value);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"Job '{jobTypeName}' requires parameters but none were found in the job data map.");

            var jobParams = JsonSerializer.Deserialize(value, jobDefinition.JobParamsType);
            if (jobParams is null)
                throw new InvalidOperationException(
                    $"Deserialized params for job '{jobTypeName}' resolved to null. Raw value: {value}");

            var paramResult = execMethod.Invoke(svc, [jobParams]);
            if (paramResult is not Task paramTask)
                throw new InvalidOperationException(
                    $"Execute on '{jobDefinition.JobType.FullName}' did not return a Task.");
            await paramTask;
        }
        else
        {
            var result = execMethod.Invoke(svc, null);
            if (result is not Task task)
                throw new InvalidOperationException(
                    $"Execute on '{jobDefinition.JobType.FullName}' did not return a Task.");
            await task;
        }
    }
}

/// <summary>
/// Single Quartz IJob wrapper for all scheduled jobs.
/// Concurrency is controlled per job via <see cref="JobBuilder.DisallowConcurrentExecution()"/>.
/// Retry logic is handled inside the executor via the self-rescheduling pattern;
/// this wrapper only surfaces non-retryable failures as <see cref="JobExecutionException"/>.
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
            // Only reached when retry is not configured.
            throw new JobExecutionException(e, refireImmediately: false);
        }
    }
}