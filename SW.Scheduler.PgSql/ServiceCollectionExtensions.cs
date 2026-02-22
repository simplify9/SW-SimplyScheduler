using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace SW.Scheduler.PgSql;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Quartz scheduler with PostgreSQL persistence.
    /// IMPORTANT: You must add Quartz tables to your DbContext using modelBuilder.UseQuartzPostgreSql(schema).
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="schema">Database schema for Quartz tables (REQUIRED for PostgreSQL)</param>
    /// <param name="configure">Optional configuration</param>
    /// <example>
    /// <code>
    /// // In your DbContext
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     modelBuilder.UseQuartzPostgreSql("quartz");
    /// }
    /// 
    /// // In Program.cs
    /// builder.Services.AddPgSqlScheduler(connectionString, "quartz");
    /// </code>
    /// </example>
    public static IServiceCollection AddPgSqlScheduler(
        this IServiceCollection services, 
        string connectionString, 
        string schema,
        Action<QuartzPgSqlOptions>? configure = null)
    {
        // Validate required parameters
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));
        
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema is required for PostgreSQL", nameof(schema));

        // Configure options
        var options = new QuartzPgSqlOptions
        {
            ConnectionString = connectionString,
            Schema = schema
        };
        configure?.Invoke(options);
        options.Validate();

        // Add the base scheduler
        services.AddScheduler();

        // Configure Quartz with PostgreSQL
        services.AddQuartz(q =>
        {
            q.UsePersistentStore(s =>
            {
                s.PerformSchemaValidation = true;
                s.UseProperties = true; // Use JobDataMap properties
                s.RetryInterval = TimeSpan.FromSeconds(15);
                
                s.UsePostgres(pg =>
                {
                    pg.UseDriverDelegate<PostgreSQLDelegate>();
                    pg.ConnectionString = options.ConnectionString;
                    pg.TablePrefix = $"{options.Schema}.{options.TablePrefix}";
                });
                
                s.UseSystemTextJsonSerializer();
                
                if (options.EnableClustering)
                {
                    s.UseClustering(c =>
                    {
                        c.CheckinMisfireThreshold = options.ClusteringMisfireThreshold;
                        c.CheckinInterval = options.ClusteringCheckinInterval;
                    });
                }
            });
        });

        // Add Quartz hosted service
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }

    /// <summary>
    /// Adds Quartz scheduler with PostgreSQL persistence using configuration action.
    /// IMPORTANT: You must add Quartz tables to your DbContext using modelBuilder.UseQuartzPostgreSql(schema).
    /// </summary>
    public static IServiceCollection AddPgSqlScheduler(
        this IServiceCollection services,
        Action<QuartzPgSqlOptions> configure)
    {
        var options = new QuartzPgSqlOptions();
        configure(options);
        options.Validate();

        return services.AddPgSqlScheduler(
            options.ConnectionString,
            options.Schema,
            configure);
    }
}

