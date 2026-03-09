using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SW.Scheduler;

internal class JobsDiscovery(IServiceProvider sp, ILogger<JobsDiscovery> logger)
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
        {
            var backgroundJobsWithParamsAsIScheduledJobOf = backgroundJobWithParams.GetType().GetTypeInfo().ImplementedInterfaces.FirstOrDefault(x =>
                x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IScheduledJob<>));
            if (backgroundJobsWithParamsAsIScheduledJobOf == null)
                continue;

            backgroundJobDefinitions.Add(new ScheduledJobDefinition
            {
                JobType = backgroundJobWithParams.GetType(),
                JobParamsType = backgroundJobsWithParamsAsIScheduledJobOf.GetGenericArguments()[0],
                ExecutMethod = backgroundJobWithParams.GetType().GetMethod(nameof(IScheduledJob.Execute)) ??
                               throw new InvalidOperationException("Execute method not found")
            });
        }

        // Warn about any duplicate group names — these would cause silent lookup collisions.
        var duplicates = backgroundJobDefinitions
            .GroupBy(d => d.Group)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicates)
            logger.LogWarning(
                "Multiple job types share the same group '{Group}'. " +
                "Ensure all job classes have unique names within their namespace segment.", dup);

        return backgroundJobDefinitions;
    }

    private IReadOnlyCollection<ScheduledJobDefinition>? _jobDefinitions;

    public IReadOnlyCollection<ScheduledJobDefinition> All
    {
        get { return _jobDefinitions ??= Load(); }
    }

    public ScheduledJobDefinition? GetJobDefinition(string jobName, string group) =>
        All.FirstOrDefault(x => x.Name == jobName && x.Group == group);

    public ScheduledJobDefinition? GetJobDefinition(Type jobType) =>
        All.FirstOrDefault(x => x.JobType == jobType);
}