using Quartz;

namespace SW.Scheduler;

public static class Extensions
{
    internal static JobBuilder WithIdentity(this JobBuilder jobBuilder, Type jobType) =>
        jobBuilder.WithIdentity(jobType.GetKey());

    internal static JobKey GetKey(this Type jobType) =>
        new(jobType.Name,
            jobType.Namespace ?? throw new InvalidOperationException("Namespace not found"));

    internal static async Task EnsureTriggerDoesNotExist(this IScheduler scheduler, TriggerKey triggerKey)
    {
        var exists = await scheduler.CheckExists(triggerKey);
        if (exists)
            throw new InvalidOperationException($"A trigger with key '{triggerKey}' already exists.");
    }

    internal static void ValidateCronExpression(this string cronExpression)
    {
        if (!CronExpression.IsValidExpression(cronExpression))
            throw new ArgumentException($"Invalid cron expression: '{cronExpression}'", nameof(cronExpression));
    }
}