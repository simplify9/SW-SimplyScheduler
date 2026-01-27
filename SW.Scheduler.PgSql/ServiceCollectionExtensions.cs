using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.AdoJobStore;
using SW.PrimitiveTypes;

namespace SW.Scheduler.PgSql;

public static class ServiceCollectionExtensions
{
    public static void AddPgSqlScheduler(this IServiceCollection services, string connectionString, string schema)
    {
        services.AddScheduler();
        services.AddQuartz(q =>
        {
            q.UsePersistentStore(s =>
            {
                
                s.PerformSchemaValidation = true; // default
                s.UseProperties = false; // preferred, but not default
                s.RetryInterval = TimeSpan.FromSeconds(15);
                s.UsePostgres(pg =>
                {
                    pg.UseDriverDelegate<PostgreSQLDelegate>();

                    pg.ConnectionString = connectionString;
                    // this is the default
                });
                s.UseSystemTextJsonSerializer();
                s.UseClustering(c =>
                {
                    
                    c.CheckinMisfireThreshold = TimeSpan.FromSeconds(20);
                    c.CheckinInterval = TimeSpan.FromSeconds(10);
                });
            });
            // base Quartz scheduler, job and trigger configuration
        });
        
    }
}