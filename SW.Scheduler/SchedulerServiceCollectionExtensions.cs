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


        services.AddSingleton<JobsDiscovery>();
        services.AddHostedService<SchedulerPreparation>();

        // Schedule repository API
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
    }
}
