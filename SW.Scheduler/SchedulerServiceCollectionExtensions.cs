using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using SW.PrimitiveTypes;
using SW.Scheduler;

namespace SW.Scheduler;

public static class SchedulerServiceCollectionExtensions
{
    public static void AddScheduler(this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0) assemblies = new[] { Assembly.GetCallingAssembly() };

        // Quartz core
        services.AddQuartz(q =>
        {
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        // Register jobs (no params + with params generics)
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo<IScheduledJob>())
            .As<IScheduledJob>().AsSelf().WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(IScheduledJob<>)))
            .AsImplementedInterfaces().AsSelf().WithScopedLifetime());

        // ensure all IScheduledJob<T> has T to be primitive type or string 
        // loop through all registered IScheduledJob<T> and check T
        var serviceProvider = services.BuildServiceProvider();
        using (var scope = serviceProvider.CreateScope())
        {
            var scheduledJobsWithParams = scope.ServiceProvider.GetServices(typeof(IScheduledJob<>));
            foreach (var job in scheduledJobsWithParams)
            {
                var jobType = job.GetType();
                var interfaceType = jobType.GetTypeInfo().ImplementedInterfaces.FirstOrDefault(x =>
                    x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IScheduledJob<>));
                if (interfaceType == null)
                    throw new SWValidationException("InvalidScheduledJob",
                        $"Scheduled job {jobType.FullName} must implement IScheduledJob<T> interface");

                var paramType = interfaceType.GetGenericArguments()[0];
                if (!(paramType.IsPrimitive || paramType == typeof(string) || paramType == typeof(decimal)))
                    throw new SWValidationException("InvalidScheduledJobParam",
                        $"Scheduled job {jobType.FullName} has invalid parameter type {paramType.FullName}. Only primitive types and string are allowed.");
            }
        }

        services.AddSingleton<JobsDiscovery>();
        services.AddHostedService<SchedulerPreparation>();

        // Schedule repository API
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
    }
}
