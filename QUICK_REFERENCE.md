# SW.Scheduler - Quick Reference

A cheat sheet for the current API. Setup, providers, the admin UI and monitoring are in [README.md](README.md);
the full guide is [SCHEDULER_GUIDE.md](SCHEDULER_GUIDE.md).

## At a glance

| | `IScheduledJob` | `IScheduledJob<TParam>` |
|---|---|---|
| Scheduled at startup with `[Schedule]` | ✅ | ❌ |
| Scheduled at runtime (`IScheduleRepository`) | ✅ one schedule per job | ✅ one schedule per `scheduleKey` |
| Parameters | ❌ | ✅ JSON-serializable `TParam` |
| Run once | ❌ | ✅ `ScheduleOnce` |

## Define a job

```csharp
[Schedule("0 0 2 * * ?", Description = "Nightly cleanup, 02:00 UTC")]
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 5)]                       // optional
[ScheduleConfig(MisfireInstructions = MisfireInstructions.Skip)]           // optional
public class CleanupJob(AppDbContext db) : IScheduledJob
{
    public Task Execute() => db.Database.ExecuteSqlRawAsync("...");
}

public record ReportParams(int CustomerId, string Format);

public class ReportJob : IScheduledJob<ReportParams>
{
    public Task Execute(ReportParams p) => Task.CompletedTask;
}
```

Job classes must be **public**: the assembly scan skips others. Dependencies are injected per run.

## Register (host project)

```csharp
// PostgreSQL. SQL Server: AddSqlServerScheduler. MySQL: AddMySqlScheduler. In-memory: AddScheduler.
builder.Services.AddPgSqlScheduler(connectionString, schema: "quartz",
    configureOptions: o => o.RetentionDays = 30,
    assemblies: typeof(CleanupJob).Assembly);
builder.Services.AddSchedulerMonitoring<AppDbContext>();

// AppDbContext.OnModelCreating, then add a migration
modelBuilder.UseSchedulerPostgreSql("quartz");
```

## Runtime API (`IScheduleRepository`)

### Simple jobs

```csharp
await scheduler.Schedule<CleanupJob>("0 0 3 * * ?");          // create or replace the trigger
await scheduler.RescheduleJob<CleanupJob>("0 0 4 * * ?");
await scheduler.PauseJob<CleanupJob>();
await scheduler.ResumeJob<CleanupJob>();
await scheduler.UnscheduleJob<CleanupJob>();                    // the job stays registered
```

### Parameterized jobs

```csharp
await scheduler.Schedule<ReportJob, ReportParams>(new(42, "pdf"), "0 0 9 * * ?", scheduleKey: "report-42");

bool created = await scheduler.ScheduleIfNotExists<ReportJob, ReportParams>(   // safe on every start, every node
    new(42, "pdf"), "0 0 9 * * ?", scheduleKey: "report-42");

string key = await scheduler.ScheduleOnce<ReportJob, ReportParams>(new(42, "pdf"));                  // now
await scheduler.ScheduleOnce<ReportJob, ReportParams>(new(42, "pdf"), runAt: DateTime.UtcNow.AddHours(1));

await scheduler.RescheduleJob<ReportJob, ReportParams>("report-42", "0 0 10 * * ?");
await scheduler.PauseJob<ReportJob, ReportParams>("report-42");
await scheduler.ResumeJob<ReportJob, ReportParams>("report-42");
await scheduler.UnscheduleJob<ReportJob, ReportParams>("report-42");
```

### Per-schedule config

```csharp
await scheduler.Schedule<CleanupJob>("0 0 3 * * ?", new ScheduleConfig
{
    AllowConcurrentExecution = false,                   // default
    RequestsRecovery         = true,                    // default: re-run if the node dies mid-run
    MisfireInstructions      = MisfireInstructions.Skip,
    EnableRetry = true, MaxRetries = 5, RetryAfterMinutes = 10
});
```

### List job types

```csharp
IEnumerable<IScheduledJobDefinition> defs = scheduler.GetJobDefinitions();
```

## Execution history (`IScheduleReader`, needs `AddSchedulerMonitoring`)

```csharp
await reader.GetLastExecution<CleanupJob>();
await reader.GetRecentExecutions<CleanupJob>(limit: 10);
await reader.GetFailedExecutions<CleanupJob>(since: DateTime.UtcNow.AddDays(-7));
await reader.GetLastExecution<ReportJob, ReportParams>("report-42");
await reader.GetRunningExecutions();                    // across all nodes
```

## Cron (6 fields, always UTC)

`second minute hour dayOfMonth month dayOfWeek`

| Expression | Meaning |
|---|---|
| `0 * * * * ?` | Every minute |
| `0 */15 * * * ?` | Every 15 minutes |
| `0 0 * * * ?` | Every hour |
| `0 0 9 * * ?` | Daily at 09:00 UTC |
| `0 0 9 * * MON-FRI` | Weekdays at 09:00 UTC |
| `0 0 0 1 * ?` | First of the month, midnight UTC |
| `0 0 0 ? * SUN` | Sundays, midnight UTC |

Triggers are built in UTC. For 09:00 at UTC+3, write `0 0 6 * * ?`.

## Things that surprise people

- **`[Schedule]` is applied again on every startup.** A cron set at runtime with `Schedule<TJob>` or
  `RescheduleJob<TJob>`, and a pause, last only until the next restart. For a schedule that must be
  changeable at runtime, leave `[Schedule]` off and schedule it from code.
- **A job's identity is its namespace (last two segments) + class name.** Renaming or moving the class makes it a
  new job; the old trigger removes itself the next time it fires.
- **There is no "run now" for a simple job.** Call the job's logic directly, or use a parameterized job and
  `ScheduleOnce`.
- **Runs don't overlap by default**, across all nodes. Set `AllowConcurrentExecution = true` to allow it.
- **A run on a node that dies is run again** on another node (`RequestsRecovery`, on by default). Jobs should be
  safe to run twice.
- `Execute()` takes no cancellation token. Shutdown waits for running jobs until the host stops waiting.

## Troubleshooting

| Symptom | Check |
|---|---|
| Job never runs | the class is public and in a scanned assembly; `GetJobDefinitions()` lists it; it has `[Schedule]` or was scheduled at runtime |
| Startup fails with a schema validation error | the migration with `UseScheduler…()` hasn't been applied; schema in `AddPgSqlScheduler` and `UseSchedulerPostgreSql` must match |
| "A trigger with key … already exists" | use `RescheduleJob`, or `ScheduleIfNotExists` for parameterized jobs |
| Runs an hour or more off | cron is UTC |
| A runtime cron change was lost | the job has `[Schedule]`, which is re-applied on startup |
| Params arrive empty or fail | `TParam` must round-trip through System.Text.Json |
