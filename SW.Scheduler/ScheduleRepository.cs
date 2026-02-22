using System.Text.Json;
using Quartz;
using SW.PrimitiveTypes;

namespace SW.Scheduler;

internal static class Constants
{
    public const string JobParamsKey = "JobParams";
}

internal class ScheduleRepository(ISchedulerFactory schedulerFactory, JobsDiscovery jobsDiscovery)
    : IScheduleRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> Schedule<TScheduler, TParam>(TParam param, string cronExpression, string triggerKey) 
        where TScheduler : IScheduledJob<TParam>
    {
        var jobDef = jobsDiscovery.GetJobDefinition(typeof(TScheduler));
        if (jobDef == null)
            throw new SWValidationException("JobNotFound", $"Job {typeof(TScheduler).Name} not found");
        
        if (param == null)
            throw new SWValidationException("JobParamsRequired", "Job params cannot be null");
        
        cronExpression.ValidateCronExpression();
        
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey = new JobKey(jobDef.Name, jobDef.Namespace);
        var triggerKeyObj = new TriggerKey(triggerKey, jobDef.Namespace);
        
        // Check if trigger already exists
        var existingTrigger = await scheduler.GetTrigger(triggerKeyObj);
        if (existingTrigger != null)
            throw new SWValidationException("TriggerAlreadyExists", $"Trigger {triggerKey} already exists");
        
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKeyObj)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression)
            .UsingJobData(Constants.JobParamsKey, JsonSerializer.Serialize(param, SerializerOptions))
            .Build();
        
        await scheduler.ScheduleJob(trigger);
        return triggerKey;
    }

    public async Task<string> Schedule<TScheduler>(string cronExpression, string triggerKey) 
        where TScheduler : IScheduledJob
    {
        var jobDef = jobsDiscovery.GetJobDefinition(typeof(TScheduler));
        if (jobDef == null)
            throw new SWValidationException("JobNotFound", $"Job {typeof(TScheduler).Name} not found");
        
        cronExpression.ValidateCronExpression();
        
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey = new JobKey(jobDef.Name, jobDef.Namespace);
        var triggerKeyObj = new TriggerKey(triggerKey, jobDef.Namespace);
        
        // Check if trigger already exists
        var existingTrigger = await scheduler.GetTrigger(triggerKeyObj);
        if (existingTrigger != null)
            throw new SWValidationException("TriggerAlreadyExists", $"Trigger {triggerKey} already exists");
        
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKeyObj)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression)
            .Build();
        
        await scheduler.ScheduleJob(trigger);
        return triggerKey;
    }

    public async Task<string> ScheduleOnce<TScheduler, TParam>(TParam param, DateTime? runAt = null) 
        where TScheduler : IScheduledJob<TParam>
    {
        var jobDef = jobsDiscovery.GetJobDefinition(typeof(TScheduler));
        if (jobDef == null)
            throw new SWValidationException("JobNotFound", $"Job {typeof(TScheduler).Name} not found");
        
        if (param == null)
            throw new SWValidationException("JobParamsRequired", "Job params cannot be null");
        
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey = new JobKey(jobDef.Name, jobDef.Namespace);
        var triggerKey = $"{jobDef.Name}_OneTime_{Guid.NewGuid():N}";
        var triggerKeyObj = new TriggerKey(triggerKey, jobDef.Namespace);
        
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKeyObj)
            .ForJob(jobKey)
            .StartAt(runAt.HasValue ? new DateTimeOffset(runAt.Value) : DateTimeOffset.UtcNow)
            .UsingJobData(Constants.JobParamsKey, JsonSerializer.Serialize(param, SerializerOptions))
            .Build();
        
        await scheduler.ScheduleJob(trigger);
        return triggerKey;
    }

    public async Task<string> ScheduleOnce<TScheduler>(DateTime? runAt = null) 
        where TScheduler : IScheduledJob
    {
        var jobDef = jobsDiscovery.GetJobDefinition(typeof(TScheduler));
        if (jobDef == null)
            throw new SWValidationException("JobNotFound", $"Job {typeof(TScheduler).Name} not found");
        
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey = new JobKey(jobDef.Name, jobDef.Namespace);
        var triggerKey = $"{jobDef.Name}_OneTime_{Guid.NewGuid():N}";
        var triggerKeyObj = new TriggerKey(triggerKey, jobDef.Namespace);
        
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKeyObj)
            .ForJob(jobKey)
            .StartAt(runAt.HasValue ? new DateTimeOffset(runAt.Value) : DateTimeOffset.UtcNow)
            .Build();
        
        await scheduler.ScheduleJob(trigger);
        return triggerKey;
    }

    public async Task RescheduleJob(string triggerKey, string newCronExpression)
    {
        newCronExpression.ValidateCronExpression();
        
        var scheduler = await schedulerFactory.GetScheduler();
        
        // Find trigger across all groups
        var triggerKeys = await scheduler.GetTriggerKeys(Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.AnyGroup());
        var existingTriggerKey = triggerKeys.FirstOrDefault(tk => tk.Name == triggerKey);
        
        if (existingTriggerKey == null)
            throw new SWValidationException("TriggerNotFound", $"Trigger {triggerKey} not found");
        
        var existingTrigger = await scheduler.GetTrigger(existingTriggerKey);
        
        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(existingTriggerKey)
            .ForJob(existingTrigger.JobKey)
            .WithCronSchedule(newCronExpression)
            .UsingJobData(existingTrigger.JobDataMap)
            .Build();
        
        await scheduler.RescheduleJob(existingTriggerKey, newTrigger);
    }

    public async Task UnscheduleJob(string triggerKey)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        
        // Find trigger across all groups
        var triggerKeys = await scheduler.GetTriggerKeys(Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.AnyGroup());
        var existingTriggerKey = triggerKeys.FirstOrDefault(tk => tk.Name == triggerKey);
        
        if (existingTriggerKey == null)
            throw new SWValidationException("TriggerNotFound", $"Trigger {triggerKey} not found");
        
        await scheduler.UnscheduleJob(existingTriggerKey);
    }

    public async Task PauseJob(string triggerKey)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        
        // Find trigger across all groups
        var triggerKeys = await scheduler.GetTriggerKeys(Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.AnyGroup());
        var existingTriggerKey = triggerKeys.FirstOrDefault(tk => tk.Name == triggerKey);
        
        if (existingTriggerKey == null)
            throw new SWValidationException("TriggerNotFound", $"Trigger {triggerKey} not found");
        
        await scheduler.PauseTrigger(existingTriggerKey);
    }

    public async Task ResumeJob(string triggerKey)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        
        // Find trigger across all groups
        var triggerKeys = await scheduler.GetTriggerKeys(Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.AnyGroup());
        var existingTriggerKey = triggerKeys.FirstOrDefault(tk => tk.Name == triggerKey);
        
        if (existingTriggerKey == null)
            throw new SWValidationException("TriggerNotFound", $"Trigger {triggerKey} not found");
        
        await scheduler.ResumeTrigger(existingTriggerKey);
    }

    public IEnumerable<IScheduledJobDefinition> GetJobDefinitions() => jobsDiscovery.All;
}