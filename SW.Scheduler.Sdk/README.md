# SW.Scheduler.Sdk

Lightweight SDK package for **SW.Scheduler** — contains all public interfaces and contracts. Reference this package in projects that **define** scheduled jobs, without pulling in any Quartz or infrastructure dependencies.

## 📦 What's in this package

- `IScheduledJob` — simple scheduled job interface
- `IScheduledJob<TParam>` — parameterized scheduled job interface
- `IScheduleRepository` — runtime scheduling and management
- `IScheduleReader` — read-only execution history queries
- `ISchedulerViewerQuery` — query interface used by the admin UI
- `ISchedulerViewerCommand` — command interface used by the admin UI
- `ScheduleAttribute` — declarative startup scheduling
- `ScheduleConfigAttribute` — concurrency and misfire configuration
- `RetryConfigAttribute` — self-rescheduling retry strategy
- `ScheduleConfig` — runtime override of scheduling options
- `SchedulerOptions` — global scheduler configuration
- `MisfireInstructions` — misfire behaviour enum
- `JobExecution` — execution history record
- `JobSummary` — job state snapshot for the admin UI

## 🎯 When to use this package

| Project type | Package |
|---|---|
| Projects that **define** jobs (`IScheduledJob`) | `SimplyWorks.Scheduler.Sdk` |
| Projects that **inject** `IScheduleRepository` | `SimplyWorks.Scheduler.Sdk` |
| Host / startup project that runs the scheduler | `SimplyWorks.Scheduler` |
| Host with PostgreSQL persistent store | `SimplyWorks.Scheduler.PgSql` |
| Host with SQL Server persistent store | `SimplyWorks.Scheduler.SqlServer` |
| Host with MySQL persistent store | `SimplyWorks.Scheduler.MySql` |
| Admin UI dashboard | `SimplyWorks.Scheduler.Viewer` |

## 📥 Installation

```bash
# In your API / job-definition project
dotnet add package SimplyWorks.Scheduler.Sdk

# In your host / startup project
dotnet add package SimplyWorks.Scheduler          # in-memory (dev/test)
dotnet add package SimplyWorks.Scheduler.PgSql    # PostgreSQL
dotnet add package SimplyWorks.Scheduler.SqlServer # SQL Server
dotnet add package SimplyWorks.Scheduler.MySql    # MySQL / MariaDB
```

## 🏗️ Architecture

```
┌──────────────────────────────────────────────────┐
│  Your API / Job-definition Project               │
│  └─ SimplyWorks.Scheduler.Sdk  (lightweight)     │
│                                                  │
│  using SW.Scheduler;                             │
│                                                  │
│  [Schedule("0 0 8 * * ?")]                       │
│  public class MyJob : IScheduledJob { ... }      │
└──────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────┐
│  Host / Startup Project                          │
│  └─ SimplyWorks.Scheduler.PgSql  (full library)  │
│                                                  │
│  builder.Services.AddPgSqlScheduler(...);        │
│  modelBuilder.UseSchedulerPostgreSql("quartz");  │
└──────────────────────────────────────────────────┘
```

## 🚀 Example usage

### Simple job (declarative scheduling via attribute)

```csharp
using SW.Scheduler;

[Schedule("0 0 2 * * ?", Description = "Nightly backup at 2 AM")]
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 5)]
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.FireOnce)]
public class BackupJob : IScheduledJob
{
    private readonly ILogger<BackupJob> _logger;

    public BackupJob(ILogger<BackupJob> logger) => _logger = logger;

    public async Task Execute()
    {
        _logger.LogInformation("Running backup...");
        await Task.CompletedTask;
    }
}
```

### Parameterized job (runtime scheduling only)

```csharp
using SW.Scheduler;

public record ReportParams(string Format, string Recipient);

[ScheduleConfig(AllowConcurrentExecution = true)]
[RetryConfig(MaxRetries = 5, RetryAfterMinutes = 10)]
public class GenerateReportJob : IScheduledJob<ReportParams>
{
    public async Task Execute(ReportParams p)
    {
        Console.WriteLine($"Generating {p.Format} report for {p.Recipient}");
        await Task.CompletedTask;
    }
}
```

### Runtime scheduling via IScheduleRepository

```csharp
using SW.Scheduler;

public class MyService(IScheduleRepository scheduler)
{
    // Simple job — override the attribute cron at runtime
    public Task OverrideBackupSchedule(string newCron)
        => scheduler.Schedule<BackupJob>(newCron);

    // Parameterized job — create a named recurring schedule
    public Task CreateReportSchedule(string scheduleKey, string cron)
        => scheduler.Schedule<GenerateReportJob, ReportParams>(
               new ReportParams("PDF", "admin@example.com"),
               cronExpression: cron,
               scheduleKey: scheduleKey);

    // One-off execution
    public Task<string> RunReportNow()
        => scheduler.ScheduleOnce<GenerateReportJob, ReportParams>(
               new ReportParams("CSV", "ops@example.com"));

    // Pause / Resume / Unschedule
    public Task Pause()  => scheduler.PauseJob<BackupJob>();
    public Task Resume() => scheduler.ResumeJob<BackupJob>();
    public Task Remove() => scheduler.UnscheduleJob<BackupJob>();
}
```

## 📋 API reference

### IScheduledJob

```csharp
public interface IScheduledJob : IScheduledJobBase
{
    Task Execute();
}
```

### IScheduledJob\<TParam\>

```csharp
public interface IScheduledJob<TParam> : IScheduledJobWithParams
{
    Task Execute(TParam jobParams);
}
```

### IScheduleRepository (key methods)

```csharp
public interface IScheduleRepository
{
    // Simple jobs
    Task Schedule<TJob>(string cronExpression, ScheduleConfig? config = null)
        where TJob : IScheduledJob;
    Task ScheduleOnce<TJob>(DateTime? runAt = null)
        where TJob : IScheduledJob;
    Task RescheduleJob<TJob>(string newCronExpression)
        where TJob : IScheduledJob;
    Task PauseJob<TJob>()   where TJob : IScheduledJob;
    Task ResumeJob<TJob>()  where TJob : IScheduledJob;
    Task UnscheduleJob<TJob>() where TJob : IScheduledJob;

    // Parameterized jobs
    Task Schedule<TJob, TParam>(TParam param, string cronExpression, string scheduleKey, ScheduleConfig? config = null)
        where TJob : IScheduledJob<TParam>;
    Task<string> ScheduleOnce<TJob, TParam>(TParam param, DateTime? runAt = null)
        where TJob : IScheduledJob<TParam>;
    Task RescheduleJob<TJob, TParam>(string scheduleKey, string newCronExpression)
        where TJob : IScheduledJob<TParam>;
    Task PauseJob<TJob, TParam>(string scheduleKey)   where TJob : IScheduledJob<TParam>;
    Task ResumeJob<TJob, TParam>(string scheduleKey)  where TJob : IScheduledJob<TParam>;
    Task UnscheduleJob<TJob, TParam>(string scheduleKey) where TJob : IScheduledJob<TParam>;

    IEnumerable<IScheduledJobDefinition> GetJobDefinitions();
}
```

### Attributes

```csharp
// Declarative startup schedule — IScheduledJob only
[Schedule("0 0 8 * * ?", Description = "Daily at 8 AM")]

// Concurrency and misfire behaviour — both job types
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.FireOnce)]

// Self-rescheduling retry — both job types
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 5)]
```

### MisfireInstructions

```csharp
public enum MisfireInstructions
{
    FireOnce, // fire once immediately on misfire (default)
    Skip,     // skip missed executions
    Ignore,   // catch up all missed executions
}
```

## 🔗 Dependencies

**None.** This is a pure interface/contract package with no external dependencies.

## 📚 Documentation

- [Main README](../README.md) — full setup guide
- [SampleApplication](../SampleApplication/README.md) — runnable reference app

## 📄 License

MIT
