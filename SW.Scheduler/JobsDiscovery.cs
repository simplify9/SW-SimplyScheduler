using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SW.PrimitiveTypes;

namespace SW.Scheduler;

internal class JobsDiscovery(IServiceProvider sp)
{
    private IReadOnlyCollection<ScheduledJobDefinition> Load()
    {
        var backgroundJobDefinitions = new List<ScheduledJobDefinition>();
        using var scope = sp.CreateScope();
        var backgroundJobs = scope.ServiceProvider.GetServices<IScheduledJob>();

        foreach (var backgroundJob in backgroundJobs)

            backgroundJobDefinitions.Add(new ScheduledJobDefinition
            {
                JobType = backgroundJob.GetType(),
                ExecutMethod = backgroundJob.GetType().GetMethod(nameof(IScheduledJob.Execute)) ??
                               throw new InvalidOperationException("Execute method not found")
            });


        var backgroundJobsWithParams = scope.ServiceProvider.GetServices<IScheduledJobWithParams>();
        foreach (var backgroundJobWithParams in backgroundJobsWithParams)
            // foreach (var jobWithParams in markedAsBackgroundJobWithParams.GetType().GetTypeInfo().ImplementedInterfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IScheduledJob<>)))
            //     backgroundJobDefinitions.Add(new BackgroundJobDefinition()
            //     {
            //         JobType = jobWithParams,
            //         JobParamsType = jobWithParams.GetGenericArguments()[0],
            //         JobParamsTypeName = jobWithParams.GetGenericArguments()[0]?.AssemblyQualifiedName ?? 
            //                             throw new InvalidOperationException("JobParamsType not found"),
            //         ExecutMethod = jobWithParams.GetMethod(nameof(IScheduledJob.Execute)) ?? 
            //                  throw new InvalidOperationException("Execute method not found")
            //     });
        {
            var backgroundJobsWithParamsAsIScheduledJobOf = backgroundJobWithParams.GetType().GetTypeInfo().ImplementedInterfaces.FirstOrDefault(x =>
                x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IScheduledJob<>));
            if(backgroundJobsWithParamsAsIScheduledJobOf == null)
                continue;
            
            
            backgroundJobDefinitions.Add(new ScheduledJobDefinition
            {
                JobType = backgroundJobWithParams.GetType(),
                JobParamsType = backgroundJobsWithParamsAsIScheduledJobOf.GetGenericArguments()[0],
                ExecutMethod = backgroundJobWithParams.GetType().GetMethod(nameof(IScheduledJob.Execute)) ??
                               throw new InvalidOperationException("Execute method not found")
            });
        }
        

        return backgroundJobDefinitions;
    }

    private IReadOnlyCollection<ScheduledJobDefinition> _jobDefinitions;

    public IReadOnlyCollection<ScheduledJobDefinition> All
    {
        get { return _jobDefinitions ??= Load(); }
    }

    public ScheduledJobDefinition GetJobDefinition(string jobName, string group) =>
        All.SingleOrDefault(x =>
            x.Name == jobName && x.Group == group);
    
    public ScheduledJobDefinition GetJobDefinition(Type jobType) =>
        All.SingleOrDefault(x => x.JobType == jobType);
}