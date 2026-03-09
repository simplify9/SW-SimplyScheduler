using System.Text.Json;
using Quartz;
using SW.PrimitiveTypes; // SWValidationException from SimplyWorks.PrimitiveTypes

namespace SW.Scheduler;

internal static class Constants
{
    public const string JobParamsKey    = "JobParams";
    public const string JobTypeNameKey  = "JobTypeName";
    public const string JobGroupKey     = "JobGroup";

    // Retry state — written/read by the executor on each attempt.
    public const string RetryCountKey        = "RetryCount";
    public const string RetryMaxKey          = "RetryMax";
    public const string RetryAfterMinutesKey = "RetryAfterMinutes";
    public const string LastErrorKey         = "LastError";

    public static string DefaultTriggerKey(string group)          => $"{group}_Default";
    public static string ParameterizedTriggerKey(string scheduleKey) => $"{scheduleKey}_Trigger";
    public static string RetryTriggerKey(string baseKey, int attempt) => $"{baseKey}_Retry_{attempt}";
}

internal static class ScheduleConfigExtensions
{
    /// <summary>
    /// Builds a <see cref="ScheduleConfig"/> from <see cref="ScheduleConfigAttribute"/>
    /// and <see cref="RetryConfigAttribute"/> on the job type, or returns defaults.
    /// </summary>
    public static ScheduleConfig FromAttribute(Type jobType)
    {
        var configAttr = jobType.GetCustomAttributes(typeof(ScheduleConfigAttribute), false)
            .FirstOrDefault() as ScheduleConfigAttribute;

        var retryAttr = jobType.GetCustomAttributes(typeof(RetryConfigAttribute), false)
            .FirstOrDefault() as RetryConfigAttribute;

        var config = configAttr == null
            ? new ScheduleConfig()
            : new ScheduleConfig
            {
                AllowConcurrentExecution = configAttr.AllowConcurrentExecution,
                RequestsRecovery         = configAttr.RequestsRecovery,
                MisfireInstructions      = configAttr.MisfireInstructions
            };

        if (retryAttr != null)
        {
            config.EnableRetry       = true;
            config.MaxRetries        = retryAttr.MaxRetries;
            config.RetryAfterMinutes = retryAttr.RetryAfterMinutes;
        }

        return config;
    }

    /// <summary>
    /// Applies <see cref="ScheduleConfig"/> to a <see cref="JobBuilder"/>:
    /// sets recovery, disallows concurrency when configured, and stores retry settings
    /// in the job's data map so the executor can access them at runtime.
    /// </summary>
    public static JobBuilder ApplyConfig(this JobBuilder builder, ScheduleConfig config)
    {
        builder = builder.RequestRecovery(config.RequestsRecovery);

        if (!config.AllowConcurrentExecution)
            builder = builder.DisallowConcurrentExecution();

        if (config.EnableRetry)
            builder = builder
                .UsingJobData(Constants.RetryMaxKey,          config.MaxRetries.ToString())
                .UsingJobData(Constants.RetryAfterMinutesKey, config.RetryAfterMinutes.ToString());

        return builder;
    }

    /// <summary>Applies misfire instructions to a <see cref="CronScheduleBuilder"/>.</summary>
    public static CronScheduleBuilder ApplyMisfire(this CronScheduleBuilder builder, MisfireInstructions misfire)
        => misfire switch
        {
            MisfireInstructions.Skip    => builder.WithMisfireHandlingInstructionDoNothing(),
            MisfireInstructions.FireAll => builder.WithMisfireHandlingInstructionIgnoreMisfires(),
            _                          => builder.WithMisfireHandlingInstructionFireAndProceed()
        };

    /// <summary>
    /// Rebuilds a cron trigger preserving its identity, job, and job data map,
    /// while applying the new cron expression and misfire instruction.
    /// </summary>
    public static ITrigger RebuildCronTrigger(ITrigger existing, string newCronExpression, MisfireInstructions misfire)
        => TriggerBuilder.Create()
            .WithIdentity(existing.Key)
            .ForJob(existing.JobKey)
            .WithCronSchedule(newCronExpression, b => b.ApplyMisfire(misfire))
            .UsingJobData(existing.JobDataMap)
            .Build();
}

internal class ScheduleRepository(ISchedulerFactory schedulerFactory, JobsDiscovery jobsDiscovery)
    : IScheduleRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void ValidateParam<TParam>(TParam param)
    {
        // Covers both reference types (null) and boxed value types passed as object.
        if (param is null)
            throw new SWValidationException("JobParamsRequired", "Job params cannot be null");
    }

    private static void ValidatePastRunAt(DateTime? runAt)
    {
        // Allow a small tolerance (5 s) for clock skew; anything older is likely a bug.
        if (runAt.HasValue && runAt.Value.ToUniversalTime() < DateTime.UtcNow.AddSeconds(-5))
            throw new SWValidationException("InvalidRunAt",
                $"runAt '{runAt.Value:O}' is in the past. Omit it to run immediately.");
    }

    private ScheduledJobDefinition RequireJobDefinition(Type jobType)
    {
        var jobDef = jobsDiscovery.GetJobDefinition(jobType);
        if (jobDef is null)
            throw new SWValidationException("JobNotFound",
                $"Job '{jobType.Name}' is not registered. Ensure it is added via AddScheduler().");
        return jobDef;
    }

    // -------------------------------------------------------------------------
    // Parameterized job – Schedule (dedicated job per scheduleKey)
    // JobKey: Name = scheduleKey, Group = jobDef.Group
    // -------------------------------------------------------------------------

    public async Task Schedule<TScheduler, TParam>(TParam param, string cronExpression, string scheduleKey, ScheduleConfig? config = null)
        where TScheduler : IScheduledJob<TParam>
    {
        ValidateParam(param);
        cronExpression.ValidateCronExpression();

        var jobDef = RequireJobDefinition(typeof(TScheduler));
        config ??= ScheduleConfigExtensions.FromAttribute(typeof(TScheduler));

        var scheduler  = await schedulerFactory.GetScheduler();
        var jobKey     = new JobKey(scheduleKey, jobDef.Group);
        var triggerKey = new TriggerKey(Constants.ParameterizedTriggerKey(scheduleKey), jobDef.Group);

        if (await scheduler.CheckExists(jobKey))
            throw new SWValidationException("ScheduleAlreadyExists",
                $"A schedule with key '{scheduleKey}' already exists for job '{typeof(TScheduler).Name}'.");

        var job = JobBuilder.Create<QuartzBackgroundJob>()
            .WithIdentity(jobKey)
            .ApplyConfig(config)
            .UsingJobData(Constants.JobTypeNameKey, jobDef.Name)
            .UsingJobData(Constants.JobGroupKey,    jobDef.Group)
            .UsingJobData(Constants.JobParamsKey,   JsonSerializer.Serialize(param, SerializerOptions))
            .StoreDurably(false)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression, b => b.ApplyMisfire(config.MisfireInstructions))
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }

    // -------------------------------------------------------------------------
    // Simple job – Schedule (single default trigger, no key exposed)
    // JobKey: Name = "MAIN", Group = jobDef.Group
    // -------------------------------------------------------------------------

    public async Task Schedule<TScheduler>(string cronExpression, ScheduleConfig? config = null)
        where TScheduler : IScheduledJob
    {
        cronExpression.ValidateCronExpression();

        var jobDef = RequireJobDefinition(typeof(TScheduler));
        config ??= ScheduleConfigExtensions.FromAttribute(typeof(TScheduler));

        var scheduler     = await schedulerFactory.GetScheduler();
        var jobKey        = new JobKey(JobKeyConventions.MainJobName, jobDef.Group);
        var triggerKeyObj = new TriggerKey(Constants.DefaultTriggerKey(jobDef.Group), jobDef.Group);

        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(triggerKeyObj)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression, b => b.ApplyMisfire(config.MisfireInstructions))
            .Build();

        // If the config (concurrency / recovery) has changed, update the stored durable job too.
        var updatedJob = JobBuilder.Create<QuartzBackgroundJob>()
            .WithIdentity(jobKey)
            .StoreDurably()
            .ApplyConfig(config)
            .Build();
        await scheduler.AddJob(updatedJob, replace: true);

        var existingTrigger = await scheduler.GetTrigger(triggerKeyObj);
        if (existingTrigger != null)
            await scheduler.RescheduleJob(triggerKeyObj, newTrigger);
        else
            await scheduler.ScheduleJob(newTrigger);
    }

    // -------------------------------------------------------------------------
    // Parameterized job – ScheduleOnce (dedicated job, auto-generated key)
    // JobKey: Name = auto-generated scheduleKey, Group = jobDef.Group
    // -------------------------------------------------------------------------

    public async Task<string> ScheduleOnce<TScheduler, TParam>(TParam param, DateTime? runAt = null, ScheduleConfig? config = null)
        where TScheduler : IScheduledJob<TParam>
    {
        ValidateParam(param);
        ValidatePastRunAt(runAt);

        var jobDef = RequireJobDefinition(typeof(TScheduler));
        config ??= ScheduleConfigExtensions.FromAttribute(typeof(TScheduler));

        var scheduler   = await schedulerFactory.GetScheduler();
        var scheduleKey = $"{jobDef.Name}_OneTime_{Guid.NewGuid():N}";
        var jobKey      = new JobKey(scheduleKey, jobDef.Group);
        var triggerKey  = new TriggerKey(Constants.ParameterizedTriggerKey(scheduleKey), jobDef.Group);

        var job = JobBuilder.Create<QuartzBackgroundJob>()
            .WithIdentity(jobKey)
            .ApplyConfig(config)
            .UsingJobData(Constants.JobTypeNameKey, jobDef.Name)
            .UsingJobData(Constants.JobGroupKey,    jobDef.Group)
            .UsingJobData(Constants.JobParamsKey,   JsonSerializer.Serialize(param, SerializerOptions))
            .StoreDurably(false)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .StartAt(runAt.HasValue ? new DateTimeOffset(runAt.Value.ToUniversalTime()) : DateTimeOffset.UtcNow)
            .Build();

        await scheduler.ScheduleJob(job, trigger);
        return scheduleKey;
    }

    // -------------------------------------------------------------------------
    // RescheduleJob
    // -------------------------------------------------------------------------

    public async Task RescheduleJob<TScheduler, TParam>(string scheduleKey, string newCronExpression)
        where TScheduler : IScheduledJob<TParam>
    {
        newCronExpression.ValidateCronExpression();

        var jobDef = RequireJobDefinition(typeof(TScheduler));

        var scheduler     = await schedulerFactory.GetScheduler();
        var triggerKeyObj = new TriggerKey(Constants.ParameterizedTriggerKey(scheduleKey), jobDef.Group);
        var existingTrigger = await scheduler.GetTrigger(triggerKeyObj)
            ?? throw new SWValidationException("ScheduleNotFound",
                $"Schedule '{scheduleKey}' not found for job '{typeof(TScheduler).Name}'.");

        // Preserve misfire config from the existing trigger where possible;
        // for simplicity we rebuild it with the job's attribute-based config.
        var config = ScheduleConfigExtensions.FromAttribute(typeof(TScheduler));
        var newTrigger = ScheduleConfigExtensions.RebuildCronTrigger(existingTrigger, newCronExpression, config.MisfireInstructions);

        await scheduler.RescheduleJob(triggerKeyObj, newTrigger);
    }

    public async Task RescheduleJob<TScheduler>(string newCronExpression)
        where TScheduler : IScheduledJob
    {
        newCronExpression.ValidateCronExpression();

        var jobDef = RequireJobDefinition(typeof(TScheduler));

        var scheduler     = await schedulerFactory.GetScheduler();
        var triggerKeyObj = new TriggerKey(Constants.DefaultTriggerKey(jobDef.Group), jobDef.Group);
        var existingTrigger = await scheduler.GetTrigger(triggerKeyObj)
            ?? throw new SWValidationException("ScheduleNotFound",
                $"No active trigger found for job '{typeof(TScheduler).Name}'. Call Schedule first.");

        var config     = ScheduleConfigExtensions.FromAttribute(typeof(TScheduler));
        var newTrigger = ScheduleConfigExtensions.RebuildCronTrigger(existingTrigger, newCronExpression, config.MisfireInstructions);

        await scheduler.RescheduleJob(triggerKeyObj, newTrigger);
    }

    // -------------------------------------------------------------------------
    // UnscheduleJob
    // -------------------------------------------------------------------------

    public async Task UnscheduleJob<TScheduler, TParam>(string scheduleKey)
        where TScheduler : IScheduledJob<TParam>
    {
        var jobDef    = RequireJobDefinition(typeof(TScheduler));
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey    = new JobKey(scheduleKey, jobDef.Group);

        if (!await scheduler.DeleteJob(jobKey))
            throw new SWValidationException("ScheduleNotFound",
                $"Schedule '{scheduleKey}' not found for job '{typeof(TScheduler).Name}'.");
    }

    public async Task UnscheduleJob<TScheduler>()
        where TScheduler : IScheduledJob
    {
        var jobDef        = RequireJobDefinition(typeof(TScheduler));
        var scheduler     = await schedulerFactory.GetScheduler();
        var triggerKeyObj = new TriggerKey(Constants.DefaultTriggerKey(jobDef.Group), jobDef.Group);

        if (!await scheduler.UnscheduleJob(triggerKeyObj))
            throw new SWValidationException("ScheduleNotFound",
                $"No active trigger found for job '{typeof(TScheduler).Name}'.");
    }

    // -------------------------------------------------------------------------
    // PauseJob
    // -------------------------------------------------------------------------

    public async Task PauseJob<TScheduler, TParam>(string scheduleKey)
        where TScheduler : IScheduledJob<TParam>
    {
        var jobDef    = RequireJobDefinition(typeof(TScheduler));
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey    = new JobKey(scheduleKey, jobDef.Group);

        if (!await scheduler.CheckExists(jobKey))
            throw new SWValidationException("ScheduleNotFound",
                $"Schedule '{scheduleKey}' not found for job '{typeof(TScheduler).Name}'.");

        await scheduler.PauseJob(jobKey);
    }

    public async Task PauseJob<TScheduler>()
        where TScheduler : IScheduledJob
    {
        var jobDef        = RequireJobDefinition(typeof(TScheduler));
        var scheduler     = await schedulerFactory.GetScheduler();
        var triggerKeyObj = new TriggerKey(Constants.DefaultTriggerKey(jobDef.Group), jobDef.Group);

        if (await scheduler.GetTrigger(triggerKeyObj) is null)
            throw new SWValidationException("ScheduleNotFound",
                $"No active trigger found for job '{typeof(TScheduler).Name}'.");

        await scheduler.PauseTrigger(triggerKeyObj);
    }

    // -------------------------------------------------------------------------
    // ResumeJob
    // -------------------------------------------------------------------------

    public async Task ResumeJob<TScheduler, TParam>(string scheduleKey)
        where TScheduler : IScheduledJob<TParam>
    {
        var jobDef    = RequireJobDefinition(typeof(TScheduler));
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey    = new JobKey(scheduleKey, jobDef.Group);

        if (!await scheduler.CheckExists(jobKey))
            throw new SWValidationException("ScheduleNotFound",
                $"Schedule '{scheduleKey}' not found for job '{typeof(TScheduler).Name}'.");

        await scheduler.ResumeJob(jobKey);
    }

    public async Task ResumeJob<TScheduler>()
        where TScheduler : IScheduledJob
    {
        var jobDef        = RequireJobDefinition(typeof(TScheduler));
        var scheduler     = await schedulerFactory.GetScheduler();
        var triggerKeyObj = new TriggerKey(Constants.DefaultTriggerKey(jobDef.Group), jobDef.Group);

        if (await scheduler.GetTrigger(triggerKeyObj) is null)
            throw new SWValidationException("ScheduleNotFound",
                $"No active trigger found for job '{typeof(TScheduler).Name}'.");

        await scheduler.ResumeTrigger(triggerKeyObj);
    }

    // -------------------------------------------------------------------------

    public IEnumerable<IScheduledJobDefinition> GetJobDefinitions() => jobsDiscovery.All;
}