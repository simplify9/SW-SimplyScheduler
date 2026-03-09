# SW.Scheduler

A type-safe, developer-friendly wrapper around [Quartz.NET](https://www.quartz-scheduler.net/) for .NET 8+ that makes job scheduling intuitive — declaratively via attributes or dynamically at runtime.

---

## 📦 Package Architecture

SW.Scheduler is split into focused packages so each project only takes the dependencies it needs.

| Package | NuGet | Use in |
|---|---|---|
| `SW.Scheduler.Sdk` | `SimplyWorks.Scheduler.Sdk` | Projects that **define** jobs (lightweight, no Quartz dependency) |
| `SW.Scheduler` | `SimplyWorks.Scheduler` | **Host/startup** project (in-memory Quartz store) |
| `SW.Scheduler.EfCore` | `SimplyWorks.Scheduler.EfCore` | Host project — adds EF Core model + job execution monitoring |
| `SW.Scheduler.PgSql` | `SimplyWorks.Scheduler.PgSql` | Host project — PostgreSQL persistent Quartz store |
| `SW.Scheduler.SqlServer` | `SimplyWorks.Scheduler.SqlServer` | Host project — SQL Server persistent Quartz store |
| `SW.Scheduler.MySql` | `SimplyWorks.Scheduler.MySql` | Host project — MySQL/MariaDB persistent Quartz store |
| `SW.Scheduler.Viewer` | `SimplyWorks.Scheduler.Viewer` | Host project — **built-in HTMX admin UI** (optional) |

> **Rule of thumb**: projects that only *define* jobs reference `SW.Scheduler.Sdk`. Only the startup/host project references a provider package (`PgSql`, `SqlServer`, or `MySql`), which pulls in `SW.Scheduler` and `SW.Scheduler.EfCore` transitively.

---

## 🚀 Quick Start

### 1. Install packages

**In projects that define jobs:**
```bash
dotnet add package SimplyWorks.Scheduler.Sdk
```

**In your host project — pick one provider:**
```bash
# PostgreSQL (most common)
dotnet add package SimplyWorks.Scheduler.PgSql
dotnet add package SimplyWorks.Scheduler.EfCore

# SQL Server
dotnet add package SimplyWorks.Scheduler.SqlServer
dotnet add package SimplyWorks.Scheduler.EfCore

# MySQL / MariaDB
dotnet add package SimplyWorks.Scheduler.MySql
dotnet add package SimplyWorks.Scheduler.EfCore

# In-memory only (development / testing)
dotnet add package SimplyWorks.Scheduler
```

**Optionally — add the built-in admin UI:**
```bash
dotnet add package SimplyWorks.Scheduler.Viewer
```

> The admin UI is entirely optional. If you prefer to build your own dashboard, skip this package and inject `ISchedulerViewerQuery` or `IScheduleReader` directly into your own controllers. See [Building a Custom UI](#-building-a-custom-ui) below.

### 2. Define a simple job

```csharp
// MyApi/Jobs/DailyReportJob.cs
// This project only needs SW.Scheduler.Sdk

using SW.PrimitiveTypes;

[Schedule("0 0 8 * * ?", Description = "Daily report at 8 AM")]
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 5)]
public class DailyReportJob : IScheduledJob
{
    private readonly IReportService _reports;

    public DailyReportJob(IReportService reports) => _reports = reports;

    public async Task Execute()
    {
        await _reports.GenerateDailyAsync();
    }
}
```

### 3. Register the scheduler (host project)

```csharp
// Program.cs

// ── PostgreSQL ────────────────────────────────────────────────────────────────
builder.Services.AddPgSqlScheduler(
    connectionString: builder.Configuration.GetConnectionString("Postgres")!,
    schema: "quartz",
    configureOptions: options =>
    {
        options.SystemUserIdentifier  = "scheduler";
        options.RetentionDays         = 30;
        options.CleanupCronExpression = "0 0 2 * * ?"; // 02:00 AM daily
        options.EnableArchive         = false;
    },
    assemblies: typeof(DailyReportJob).Assembly
);

// ── EF Core monitoring store ──────────────────────────────────────────────────
builder.Services.AddSchedulerMonitoring<AppDbContext>();
```

### 4. Add scheduler tables to your DbContext

```csharp
// AppDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Pick the method that matches your provider:
    modelBuilder.UseQuartzPostgreSql("quartz");   // SW.Scheduler.PgSql
    // modelBuilder.UseQuartzSqlServer();          // SW.Scheduler.SqlServer
    // modelBuilder.UseQuartzSqlServer("myschema");
    // modelBuilder.UseQuartzMySql();              // SW.Scheduler.MySql
}
```

Then add and apply a migration as normal:
```bash
dotnet ef migrations add AddScheduler
dotnet ef database update
```

---

## 🖥️ Admin UI (`SW.Scheduler.Viewer`)

`SW.Scheduler.Viewer` ships a lightweight, server-rendered dashboard built with [HTMX](https://htmx.org/) and [Pico.css](https://picocss.com/). It mounts at a configurable path (default `/scheduler-management`) and requires **no JavaScript framework**.

**Features:**
- Live dashboard — currently running jobs, recent executions, success rate
- Execution history with filtering by job group and status
- Per-execution detail view including job parameters (context) and error messages
- Auto-refreshing via HTMX partial swaps (no full page reloads)

### Installation

```bash
dotnet add package SimplyWorks.Scheduler.Viewer
```

> Requires `SimplyWorks.Scheduler.EfCore` and `AddSchedulerMonitoring<TDbContext>()` to be registered first — the viewer reads from the `job_executions` table.

### Wiring it up

```csharp
// Program.cs

// 1. Register services — call before builder.Build()
builder.Services.AddControllersWithViews(); // required if not already added
builder.Services.AddSchedulerMonitoring<AppDbContext>(); // must come first

builder.Services.AddSchedulerViewer(opts =>
{
    opts.PathPrefix = "/scheduler-management"; // default — change as needed
    opts.Title      = "My App Scheduler";      // shown in the browser tab and header
});

var app = builder.Build();

// 2. Wire middleware and routes — call after UseRouting (implicit in WebApplication)
app.UseSchedulerViewer();  // auth guard middleware
app.MapSchedulerViewer();  // MVC routes under PathPrefix
app.MapControllers();

app.Run();
```

### Authentication

The viewer has **no built-in authentication** — you supply the authorization logic via a delegate. This keeps the package decoupled from your auth stack (Identity, JWT, API keys, sessions, etc.).

Set `AuthorizeAsync` on the options. It receives the `HttpContext` and must return `true` to allow or `false` to respond with `401 Unauthorized`.

#### ASP.NET Core Identity / role check

```csharp
builder.Services.AddSchedulerViewer(opts =>
{
    opts.AuthorizeAsync = ctx =>
        Task.FromResult(ctx.User.Identity?.IsAuthenticated == true
                     && ctx.User.IsInRole("Admin"));
});
```

#### API key header

```csharp
builder.Services.AddSchedulerViewer(opts =>
{
    opts.AuthorizeAsync = ctx =>
    {
        var key = ctx.Request.Headers["X-Scheduler-Key"].FirstOrDefault();
        return Task.FromResult(key == configuration["Scheduler:AdminKey"]);
    };
});
```

#### Cookie / session token

```csharp
builder.Services.AddSchedulerViewer(opts =>
{
    opts.AuthorizeAsync = ctx =>
        Task.FromResult(ctx.Request.Cookies["scheduler_auth"] == "my-secret-token");
});
```

#### Policy-based (ASP.NET Core authorization middleware)

If you'd rather use `[Authorize]` policies from the built-in middleware, configure `AuthorizeAsync` to call `IAuthorizationService`:

```csharp
builder.Services.AddSchedulerViewer(opts =>
{
    opts.AuthorizeAsync = async ctx =>
    {
        var authService = ctx.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authService.AuthorizeAsync(ctx.User, "SchedulerAdminPolicy");
        return result.Succeeded;
    };
});
```

> ⚠️ **Never leave `AuthorizeAsync` as `null` in production.** When `null` all requests are allowed — this is intentional for local development only.

### `SchedulerViewerOptions` reference

| Option | Default | Description |
|---|---|---|
| `PathPrefix` | `"/scheduler-management"` | URL path where the UI is mounted |
| `Title` | `"Scheduler"` | Title in the browser tab and page header |
| `DefaultPageSize` | `50` | Number of rows shown on the History page |
| `AuthorizeAsync` | `null` *(allow all)* | Async delegate returning `true` to allow a request |

---

## 🔧 Building a Custom UI

Don't want the built-in viewer? Skip `SimplyWorks.Scheduler.Viewer` entirely and build your own dashboard using the two query interfaces exposed by the library.

### Option A — `IScheduleReader` (type-safe, generic)

Inject `IScheduleReader` for strongly-typed queries scoped to a specific job type. Requires `SW.Scheduler.EfCore`.

```csharp
// In your own controller or Razor Page
public class MyDashboardController(IScheduleReader reader) : Controller
{
    public async Task<IActionResult> Index()
    {
        var running = await reader.GetRunningExecutions();
        var recent  = await reader.GetRecentExecutions<DailyReportJob>(limit: 20);
        var failed  = await reader.GetFailedExecutions<DailyReportJob>(
                          since: DateTime.UtcNow.AddDays(-7));
        // ...
    }
}
```

### Option B — `ISchedulerViewerQuery` (non-generic, UI-friendly)

Inject `ISchedulerViewerQuery` for runtime queries without knowing job types at compile time — ideal for a generic dashboard. Registered automatically by `AddSchedulerMonitoring<TDbContext>()`.

```csharp
public class MyDashboardController(ISchedulerViewerQuery query) : Controller
{
    public async Task<IActionResult> Index()
    {
        var running = await query.GetRunningAsync();
        var recent  = await query.GetRecentAsync(limit: 50);
        var history = await query.GetHistoryAsync(jobGroup: null, success: false, limit: 20);
        var detail  = await query.GetByFireInstanceIdAsync("some-fire-id");
        // ...
    }
}
```

Both interfaces work with any database provider (PostgreSQL, SQL Server, MySQL) — the implementation is in `SW.Scheduler.EfCore`.

---

## 🎯 Job Types

| Interface | Scheduling | Parameters | Attribute support |
|---|---|---|---|
| `IScheduledJob` | Startup (attribute) **or** runtime API | None | ✅ `[Schedule]`, `[RetryConfig]`, `[ScheduleConfig]` |
| `IScheduledJob<TParam>` | Runtime API only | ✅ Per-schedule | ❌ (runtime-only) |

---

## ✍️ Attributes (simple jobs only)

### `[Schedule]` — declarative cron trigger
```csharp
[Schedule("0 0 2 * * ?", Description = "Cleanup at 2 AM")]
public class CleanupJob : IScheduledJob { ... }
```
The trigger can be overridden at runtime via `IScheduleRepository.Schedule<TJob>(cronExpression)`.

### `[RetryConfig]` — self-rescheduling retry
```csharp
[RetryConfig(MaxRetries = 5, RetryAfterMinutes = 10)]
public class CleanupJob : IScheduledJob { ... }
```
On failure the job catches the exception, increments a counter in the data map, and schedules a one-time trigger at `now + RetryAfterMinutes`. Quartz never sees the failure.

### `[ScheduleConfig]` — concurrency & misfire behaviour
```csharp
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.Skip)]
public class CleanupJob : IScheduledJob { ... }
```

---

## ⚙️ Scheduler Options

Passed to `AddScheduler(options => ...)` or any provider's `configureOptions` parameter.

| Option | Default | Description |
|---|---|---|
| `SystemUserIdentifier` | `"scheduled-job"` | Identity name set on `RequestContext` during execution |
| `RetentionDays` | `30` | Days to keep `JobExecution` rows before the cleanup job deletes them |
| `CleanupCronExpression` | `"0 0 2 * * ?"` | When the cleanup job runs (daily at 2 AM by default) |
| `EnableArchive` | `false` | Upload execution JSON to `ICloudFilesService` after each run |
| `CloudFilesPrefix` | `""` | Key prefix for archived files, e.g. `"my-app/"` |

---

## 📅 Runtime Scheduling API (`IScheduleRepository`)

Inject `IScheduleRepository` anywhere to manage schedules dynamically.

### Simple jobs

```csharp
// Override the attribute-defined schedule at runtime
await _scheduler.Schedule<DailyReportJob>("0 0 9 * * ?");

// Reschedule
await _scheduler.RescheduleJob<DailyReportJob>("0 0 10 * * ?");

// Pause / Resume
await _scheduler.PauseJob<DailyReportJob>();
await _scheduler.ResumeJob<DailyReportJob>();

// Remove trigger (job stays registered)
await _scheduler.UnscheduleJob<DailyReportJob>();
```

### Parameterized jobs

```csharp
public record NotifyParams(int CustomerId, string Template);

public class NotifyCustomerJob : IScheduledJob<NotifyParams>
{
    public async Task Execute(NotifyParams p)
    {
        // send notification to p.CustomerId using p.Template
    }
}

// Each scheduleKey is an independent Quartz job with its own data
await _scheduler.Schedule<NotifyCustomerJob, NotifyParams>(
    param: new NotifyParams(42, "welcome"),
    cronExpression: "0 0 9 * * ?",
    scheduleKey: "notify-customer-42"
);

// Run once immediately (or at a specific time)
var key = await _scheduler.ScheduleOnce<NotifyCustomerJob, NotifyParams>(
    param: new NotifyParams(42, "reminder"),
    runAt: DateTime.UtcNow.AddHours(1)
);

// Reschedule / pause / resume / remove by scheduleKey
await _scheduler.RescheduleJob<NotifyCustomerJob, NotifyParams>("notify-customer-42", "0 0 10 * * ?");
await _scheduler.PauseJob<NotifyCustomerJob, NotifyParams>("notify-customer-42");
await _scheduler.ResumeJob<NotifyCustomerJob, NotifyParams>("notify-customer-42");
await _scheduler.UnscheduleJob<NotifyCustomerJob, NotifyParams>("notify-customer-42");
```

### Per-schedule config override

```csharp
await _scheduler.Schedule<NotifyCustomerJob, NotifyParams>(
    param: new NotifyParams(42, "welcome"),
    cronExpression: "0 0 9 * * ?",
    scheduleKey: "notify-42",
    config: new ScheduleConfig
    {
        AllowConcurrentExecution = true,
        MisfireInstructions      = MisfireInstructions.Skip,
        Retry = new RetryConfig { MaxRetries = 5, RetryAfterMinutes = 15 }
    }
);
```

---

## 📊 Job Execution Monitoring

Requires `SW.Scheduler.EfCore` + `AddSchedulerMonitoring<TDbContext>()`.

Every job execution is automatically recorded in the `job_executions` table. Inject `IScheduleReader` to query history:

```csharp
// Simple job
var last    = await _reader.GetLastExecution<DailyReportJob>();
var recent  = await _reader.GetRecentExecutions<DailyReportJob>(limit: 10);
var failed  = await _reader.GetFailedExecutions<DailyReportJob>(since: DateTime.UtcNow.AddDays(-7));

// Parameterized job (by scheduleKey)
var last    = await _reader.GetLastExecution<NotifyCustomerJob, NotifyParams>("notify-42");
var recent  = await _reader.GetRecentExecutions<NotifyCustomerJob, NotifyParams>("notify-42", limit: 10);
var failed  = await _reader.GetFailedExecutions<NotifyCustomerJob, NotifyParams>("notify-42");

// All currently running jobs (across cluster nodes)
var running = await _reader.GetRunningExecutions();
```

### `JobExecution` record fields

| Field | Description |
|---|---|
| `Id` | Auto-increment PK |
| `JobName` / `JobGroup` / `JobTypeName` | Job identifier |
| `FireInstanceId` | Unique per execution (cluster-safe) |
| `StartTimeUtc` / `EndTimeUtc` / `DurationMs` | Timing |
| `Success` / `Error` | Outcome |
| `Node` | `Environment.MachineName` of the node that ran the job |
| `Context` | JSON-serialized `ScheduledJobContext` (contains `JobParameter` for parameterized jobs) |

### Cloud archiving (optional)

Set `EnableArchive = true` and register `ICloudFilesService` (from `SimplyWorks.PrimitiveTypes`).  
Each execution is uploaded to:
```
{CloudFilesPrefix}job-history/{JobGroup}/{yyyy}/{MM}/{dd}/{FireInstanceId}.json
```

---

## 🗄️ Provider Reference

### PostgreSQL — `SW.Scheduler.PgSql`

```csharp
// Program.cs
services.AddPgSqlScheduler(
    connectionString: "Host=...;Database=...;",
    schema: "quartz",                   // required
    configure: o => {
        o.EnableClustering = true;      // optional
    }
);

// AppDbContext.OnModelCreating
modelBuilder.UseQuartzPostgreSql("quartz");
```

### SQL Server — `SW.Scheduler.SqlServer`

```csharp
// Program.cs
services.AddSqlServerScheduler(
    connectionString: "Server=...;Database=...;",
    configure: o => {
        o.Schema      = "dbo";          // optional, default: "dbo"
        o.TablePrefix = "QRTZ_";       // optional, default: "QRTZ_"
    }
);

// AppDbContext.OnModelCreating
modelBuilder.UseQuartzSqlServer();              // dbo schema
modelBuilder.UseQuartzSqlServer("scheduler");   // explicit schema
```

### MySQL / MariaDB — `SW.Scheduler.MySql`

```csharp
// Program.cs
services.AddMySqlScheduler(
    connectionString: "Server=...;Database=...;",
    configure: o => {
        o.TablePrefix = "QRTZ_";       // optional, default: "QRTZ_"
    }
);

// AppDbContext.OnModelCreating
modelBuilder.UseQuartzMySql();
```

### In-Memory — `SW.Scheduler` (development / tests)

```csharp
services.AddScheduler(
    options => { options.RetentionDays = 7; },
    assemblies: typeof(MyJob).Assembly
);
```

---

## 📅 Cron Expression Format

Quartz.NET uses 6-field cron: `second minute hour dayOfMonth month dayOfWeek`

| Expression | Meaning |
|---|---|
| `0 * * * * ?` | Every minute |
| `0 0 * * * ?` | Every hour |
| `0 0 8 * * ?` | Daily at 8 AM |
| `0 0 8 * * MON-FRI` | Weekdays at 8 AM |
| `0 */15 * * * ?` | Every 15 minutes |
| `0 0 0 1 * ?` | First of every month at midnight |

---

## 🎓 Best Practices

**✅ DO**
- Use `[Schedule]` for fixed, predictable schedules on `IScheduledJob`
- Use runtime API (`IScheduleRepository`) for user-configurable or data-driven schedules
- Use `[RetryConfig]` for jobs that call external services and may fail transiently
- Use `[ScheduleConfig(AllowConcurrentExecution = false)]` (the default) to prevent overlap
- Keep job `Execute` methods focused; inject services via constructor

**❌ DON'T**
- Don't apply `[Schedule]` to `IScheduledJob<TParam>` — parameterized jobs are runtime-only
- Don't use the same `scheduleKey` for two different jobs
- Don't reference `SW.Scheduler` (or any provider package) from job-definition projects — `SW.Scheduler.Sdk` is enough

---

## 📄 License

MIT
