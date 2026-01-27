using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quartz;

namespace SW.Scheduler;

[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
internal class QuartzBackgroundJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly JobsDiscovery _jobsDiscovery;
    private readonly ILogger<QuartzBackgroundJob> _logger;
    public QuartzBackgroundJob(IServiceProvider serviceProvider, JobsDiscovery jobsDiscovery, ILogger<QuartzBackgroundJob> logger)
    {
        _serviceProvider = serviceProvider;
        _jobsDiscovery = jobsDiscovery;
        _logger = logger;
    }

    public Task Execute(IJobExecutionContext context) => TryExecuteInternal(context);

    private async Task TryExecuteInternal(IJobExecutionContext context)
    {
        try
        {
            await ExecuteInternal(context);
        }
        catch (Exception e)
        {
            throw new JobExecutionException(e, false);
        }
    }

    private async Task ExecuteInternal(IJobExecutionContext context)
    {
        var jobName = context.JobDetail.Key.Name;
        var jobNameSpace = context.JobDetail.Key.Group;
        
        var jobDefinition = _jobsDiscovery.GetJobDefinition(jobName, jobNameSpace);
        if (jobDefinition == null)
        {
            _logger.LogError($"Job {jobName} with namespace {jobNameSpace} not found in jobs discovery");
            await context.Scheduler.UnscheduleJob(context.Trigger.Key);
            return;
        }
        
        
        var svc = _serviceProvider.GetService(jobDefinition.JobType);
        
        if (svc == null)
        {
            _logger.LogError($"Service {jobDefinition.JobType.FullName} not found in service provider");
            return;
        }
        var execMethod = jobDefinition.ExecutMethod;
        
        if (jobDefinition.WithParams)
        {
            var exists = context.MergedJobDataMap.TryGetString(Constants.JobParamsKey, out var value);
            
            if (!exists || string.IsNullOrWhiteSpace(value))
            {
                _logger.LogError($"Job {jobName} with namespace {jobNameSpace} requires parameters but not found in job data map");
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