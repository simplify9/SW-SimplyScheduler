# SW.Scheduler — Implementation Guide

## Overview

SW.Scheduler is a type-safe scheduling library built on [Quartz.NET](https://www.quartz-scheduler.net/). It supports declarative scheduling via attributes and full runtime control via `IScheduleRepository`, with built-in job execution monitoring, retry strategies, and an optional admin UI.

---

## Namespace

All public types live in **`SW.Scheduler`**:

```csharp
using SW.Scheduler;
```

---

## Job Types

### `IScheduledJob` — simple job, no parameters

```csharp
using SW.Scheduler;

[Schedule("0 0 8 * * ?", Description = "Daily report at 8 AM")]
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 5)]
[ScheduleConfig(AllowConcurrentExecution = false)]
public class DailyReportJob : IScheduledJob
{
    private readonly ILogger<DailyReportJob> _logger;

    public DailyReportJob(ILogger<DailyReportJob> logger) => _logger = logger;

    public async Task Execute()
    {
        _logger.LogInformation("Running daily report...");
        await Task.CompletedTask;
    }
}
```

- Scheduled automatically on startup via `[Schedule]`.
- Supports only **one** active cron trigger at a time.
- The schedule can be overridden at runtime via `IScheduleRepository.Schedule<TJob>()`.

### `IScheduledJob<TParam>` — parameterized job

```csharp
using SW.Scheduler;

public record EmailParams(string Group, string Template);

[ScheduleConfig(AllowConcurrentExecution = true)]
[RetryConfig(MaxRetries = 5, RetryAfterMinutes = 10)]
public class SendEmailsJob : IScheduledJob<EmailParams>
{
    public async Task Execute(EmailParams p)
    {
        Console.WriteLine($"Sending '{p.Template}' to group '{p.Group}'");
        await Task.CompletedTask;
    }
}
```

- **No** `[Schedule]` attribute — always scheduled at runtime.
- Each `scheduleKey` gets its own dedicated scheduler entry, enabling multiple concurrent configurations with different parameters.

---

## Attributes

| Attribute | Applies to | Purpose |
|---|---|---|
| `[Schedule(cron)]` | `IScheduledJob` only | Declarative startup scheduling |
| `[ScheduleConfig(...)]` | Both | Concurrency, recovery, misfire behaviour |
| `[RetryConfig(...)]` | Both | Self-rescheduling retry on exception |

### `[ScheduleConfig]`

```csharp
[ScheduleConfig(
    AllowConcurrentExecution = false,   // default: false
    RequestsRecovery = true,            // default: true — re-run after crash
    MisfireInstructions = MisfireInstructions.FireOnce  // default
)]
```

### `[RetryConfig]`

When a job throws an exception, the scheduler:
1. Catches the exception and saves it to the job's data map.
2. Increments a persistent `RetryCount`.
3. If `RetryCount < MaxRetries`, schedules a one-time trigger at `now + RetryAfterMinutes`.
4. Lets the current execution finish cleanly (no error state).

```csharp
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 5)]
```

---

## IScheduleRepository

Inject `IScheduleRepository` anywhere in your application to manage schedules at runtime.

### Simple job — Schedule

```csharp
// Create or replace the default trigger
await scheduler.Schedule<DailyReportJob>("0 0 8 * * ?");

// With runtime config override
await scheduler.Schedule<DailyReportJob>("0 0 8 * * ?", new ScheduleConfig
{
    EnableRetry = true,
    MaxRetries = 5,
    RetryAfterMinutes = 10
});
```

### Simple job — ScheduleOnce

```csharp
// Run immediately
await scheduler.ScheduleOnce<DailyReportJob>();

// Run at a specific UTC time
await scheduler.ScheduleOnce<DailyReportJob>(runAt: DateTime.UtcNow.AddHours(1));
```

### Parameterized job — Schedule

```csharp
// Create a named recurring schedule
await scheduler.Schedule<SendEmailsJob, EmailParams>(
    param: new EmailParams("VIP", "VipNewsletter"),
    cronExpression: "0 0 9 * * ?",   // daily at 9 AM
    scheduleKey: "vip-emails-daily"
);

// A second independent schedule for a different group
await scheduler.Schedule<SendEmailsJob, EmailParams>(
    param: new EmailParams("Regular", "RegularNewsletter"),
    cronExpression: "0 0 10 * * ?",
    scheduleKey: "regular-emails-daily"
);
```

### Parameterized job — ScheduleOnce

```csharp
// Returns the auto-generated schedule key
string key = await scheduler.ScheduleOnce<SendEmailsJob, EmailParams>(
    param: new EmailParams("Trial", "WelcomeEmail")
);
```

### Reschedule

```csharp
// Simple job — replace its cron expression
await scheduler.RescheduleJob<DailyReportJob>("0 0 6 * * ?");

// Parameterized — update a named schedule
await scheduler.RescheduleJob<SendEmailsJob, EmailParams>("vip-emails-daily", "0 0 7 * * ?");
```

### Pause / Resume

```csharp
// Simple job
await scheduler.PauseJob<DailyReportJob>();
await scheduler.ResumeJob<DailyReportJob>();

// Parameterized — by schedule key
await scheduler.PauseJob<SendEmailsJob, EmailParams>("vip-emails-daily");
await scheduler.ResumeJob<SendEmailsJob, EmailParams>("vip-emails-daily");
```

### Unschedule

```csharp
// Simple job — removes the trigger, keeps the durable job registration
await scheduler.UnscheduleJob<DailyReportJob>();

// Parameterized — removes the dedicated job entry entirely
await scheduler.UnscheduleJob<SendEmailsJob, EmailParams>("vip-emails-daily");
```

### Discovery

```csharp
IEnumerable<IScheduledJobDefinition> defs = scheduler.GetJobDefinitions();
foreach (var def in defs)
    Console.WriteLine($"{def.Name} ({def.Group})");
```

---

## ScheduleConfig (runtime override)

`ScheduleConfig` can be passed to any `Schedule*` call to override the job's attribute-based defaults:

```csharp
var config = new ScheduleConfig
{
    AllowConcurrentExecution = true,
    RequestsRecovery         = false,
    MisfireInstructions      = MisfireInstructions.Skip,
    EnableRetry              = true,
    MaxRetries               = 5,
    RetryAfterMinutes        = 15
};

await scheduler.Schedule<DailyReportJob>("0 0 8 * * ?", config);
```

---

## Cron Expression Format

SW.Scheduler uses 6-field cron syntax (powered by Quartz.NET):

```
second  minute  hour  dayOfMonth  month  dayOfWeek
```

| Expression | Meaning |
|---|---|
| `0 * * * * ?` | Every minute |
| `0 0 * * * ?` | Every hour |
| `0 0 8 * * ?` | Daily at 08:00 |
| `0 0 8 * * MON-FRI` | Weekdays at 08:00 |
| `0 */15 * * * ?` | Every 15 minutes |
| `0 0 0 1 * ?` | First day of every month at midnight |

---

## Job Identity Convention

Job keys are derived from the job type automatically:

- **Group** = last two namespace segments + class name
  e.g. `SampleApplication.Jobs.SendEmailsJob` → group `Jobs.SendEmailsJob`
- **Name (simple job)** = `"MAIN"`
- **Name (parameterized)** = the `scheduleKey` you provide

---

## Monitoring

When `AddSchedulerMonitoring<TDbContext>()` is called, every execution is automatically recorded in a `job_executions` table:

```csharp
// Inject IScheduleReader for dashboard queries
public class DashboardService(IScheduleReader reader)
{
    public Task<JobExecution?> LastRun()
        => reader.GetLastExecution<DailyReportJob>();

    public Task<IReadOnlyList<JobExecution>> Recent()
        => reader.GetRecentExecutions<DailyReportJob>(limit: 10);

    public Task<IReadOnlyList<JobExecution>> Failures()
        => reader.GetFailedExecutions<DailyReportJob>(since: DateTime.UtcNow.AddDays(-7));

    public Task<IReadOnlyList<JobExecution>> Running()
        => reader.GetRunningExecutions();
}
```

---

## Setup Summary

```csharp
// Program.cs
using SW.Scheduler;
using SW.Scheduler.PgSql;  // or SqlServer / MySql

// 1. Register the scheduler (provider package does it all)
builder.Services.AddPgSqlScheduler(
    connectionString: builder.Configuration.GetConnectionString("Postgres")!,
    schema: "quartz",
    configureOptions: o =>
    {
        o.SystemUserIdentifier = "scheduler";
        o.RetentionDays        = 30;
        o.EnableArchive        = false;
    },
    assemblies: typeof(Program).Assembly
);

// 2. Register EF Core monitoring (optional)
builder.Services.AddSchedulerMonitoring<AppDbContext>();

// 3. Register admin UI (optional)
builder.Services.AddSchedulerViewer(o =>
{
    o.PathPrefix    = "/scheduler";
    o.AuthorizeAsync = ctx => Task.FromResult(ctx.User.Identity?.IsAuthenticated == true);
});

var app = builder.Build();

app.UseSchedulerViewer();
app.MapSchedulerViewer();

app.Run();
```

```csharp
// AppDbContext.OnModelCreating
modelBuilder.UseSchedulerPostgreSql("quartz");
// or: modelBuilder.UseSchedulerSqlServer()
// or: modelBuilder.UseSchedulerMySql()
```

---

## Best Practices

- **Keep jobs focused** — one responsibility per job class.
- **Use `[Schedule]` for system-defined schedules** — things that always run regardless of user configuration.
- **Use runtime scheduling for user-driven schedules** — dynamic cron expressions, different parameter values.
- **Use `[RetryConfig]`** for jobs that depend on external services that may be temporarily unavailable.
- **Avoid shared mutable state** — each job execution gets its own DI scope.
- **Log liberally inside `Execute()`** — the monitoring layer captures success/failure but not internal steps.
