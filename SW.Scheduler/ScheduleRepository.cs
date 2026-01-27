using Quartz;
using SW.PrimitiveTypes;
using SW.Scheduler;

namespace SW.Scheduler;

internal static class Constants
{
    public const string JobParamsKey = "params";
    
}

internal class ScheduleRepository(ISchedulerFactory schedulerFactory, JobsDiscovery jobsDiscovery)
    : IScheduleRepository
{
    
    public async Task Schedule<TScheduler, TParam>(TParam param, string key, string cronExpression) where TScheduler : IScheduledJob<TParam>
    {
        var jobDef = jobsDiscovery.All.FirstOrDefault(j => j.JobType == typeof(TScheduler));
        if(jobDef == null)
            throw new SWValidationException("JobNotFound", "Job not found");
        if(param == null)
            throw new SWValidationException("JobParamsRequired", "Job params required");
        cronExpression.ValidateCronExpression();
        
        var scheduler = await schedulerFactory.GetScheduler();
        var (jobKey, triggerKey) = await ResolveAndEnsureDoesNotExist(scheduler, jobDef.JobType, key);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression)
            .Build();
        trigger.JobDataMap.Put(Constants.JobParamsKey, param);
        await scheduler.ScheduleJob(trigger);
    }

    public async Task Schedule<TSheduler>(string cronExpression) where TSheduler : IScheduledJob
    {
        var jobDef = jobsDiscovery.All.FirstOrDefault(j => j.JobType == typeof(TSheduler));
        if(jobDef == null)
            throw new SWValidationException("JobNotFound", $"Job  not found");
        
        cronExpression.ValidateCronExpression();
        
        var scheduler = await schedulerFactory.GetScheduler();
        var key = jobDef.JobType.Name;
        var (jobKey, triggerKey) = await ResolveAndEnsureDoesNotExist(scheduler, jobDef.JobType, key);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression)
            .Build();
        await scheduler.ScheduleJob(trigger);
    }

    public async Task ScheduleOnce(string name, string key, object jobParams, DateTime? runAt = null)
    {
        var jobDef = jobsDiscovery.All.FirstOrDefault(j => j.JobType.Name == name);
        if(jobDef == null)
            throw new SWValidationException("JobNotFound", $"Job '{name}' not found");
        if(jobParams == null)
            throw new SWValidationException("JobParamsRequired", "Job params required");
        
        var scheduler = await schedulerFactory.GetScheduler();
        var (jobKey, triggerKey) = await ResolveAndEnsureDoesNotExist(scheduler, jobDef.JobType, key);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .StartAt(runAt ?? DateTime.UtcNow)
            .Build();
        trigger.JobDataMap.Put(Constants.JobParamsKey, jobParams);
        await scheduler.ScheduleJob(trigger);
    }

    private async Task<(JobKey jobKey , TriggerKey triggerKey)> ResolveAndEnsureDoesNotExist(IScheduler scheduler, Type jobType, string key)
    {
        var jobKey = jobType.GetKey();
        var triggerKey = new TriggerKey(key, jobKey.Group);
        await scheduler.EnsureTriggerDoesNotExist(triggerKey);
        
        return (jobKey,triggerKey);
    }

    public IEnumerable<IScheduledJobDefinition> GetBackgroundJobDefinitions() => jobsDiscovery.All;
}