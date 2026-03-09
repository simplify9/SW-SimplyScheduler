# SampleApplication

A runnable reference application that demonstrates every feature of **SW.Scheduler** in one place.

The app intentionally ships **two parallel UI approaches** side-by-side:

| Approach | What it is | When to use |
|---|---|---|
| **Built-in Admin UI** (`/scheduler-management`) | Server-rendered HTMX + Pico.css dashboard from `SW.Scheduler.Viewer` | Zero-effort dashboard — install the NuGet and wire two lines |
| **REST API** (`/api/scheduler`, `/api/dashboard`) | JSON endpoints exposed via `SchedulerController` and `DashboardController` | Foundation for building your own SPA, mobile app, or custom admin UI |

Both approaches are fully wired and running concurrently so you can compare them directly and choose the one that fits your project.

---

## ▶️ Running the App

```bash
cd SampleApplication
dotnet run
```

The app starts on `https://localhost:5001` (or the port shown in the console).

| URL | Description |
|---|---|
| `https://localhost:5001/scheduler-management` | Built-in HTMX admin UI |
| `https://localhost:5001/swagger` | Swagger UI for all REST endpoints |

Five sample customers (`Alice`, `Bob`, `Carol`, `Dan`, `Eva`) are seeded automatically on first run.

---

## 📦 Project References

The sample uses an **in-memory Quartz store** and an **in-memory EF Core database** so it runs with zero infrastructure — no PostgreSQL, no SQL Server, no Redis needed.

```xml
<!-- SampleApplication.csproj -->
<ProjectReference Include="..\SW.Scheduler\SW.Scheduler.csproj" />
<ProjectReference Include="..\SW.Scheduler.EfCore\SW.Scheduler.EfCore.csproj" />
<ProjectReference Include="..\SW.Scheduler.Sdk\SW.Scheduler.Sdk.csproj" />
<ProjectReference Include="..\SW.Scheduler.PgSql\SW.Scheduler.PgSql.csproj" />
<ProjectReference Include="..\SW.Scheduler.Viewer\SW.Scheduler.Viewer.csproj" />
```

> In a real application you would reference only the provider you need (`PgSql`, `SqlServer`, or `MySql`) and `SW.Scheduler.EfCore`. Job-definition projects reference only `SW.Scheduler.Sdk`.

---

## 🗂️ Project Structure

```
SampleApplication/
├── Jobs/
│   ├── CountCustomersJob.cs        — simple job, [Schedule] attribute, [RetryConfig]
│   ├── GenerateReportJob.cs        — simple job, [Schedule] attribute, no retry
│   ├── SendCustomerEmailsJob.cs    — parameterized job (SendEmailsParams)
│   └── NotifyCustomerJob.cs        — parameterized job (NotifyCustomerParams)
│
├── Controllers/
│   ├── SchedulerController.cs      — REST API: schedule / reschedule / pause / resume / unschedule
│   ├── DashboardController.cs      — REST API: execution history queries (IScheduleReader)
│   └── CustomersController.cs      — CRUD for the sample Customer entity
│
├── Data/
│   └── AppDbContext.cs             — EF Core DbContext (in-memory + ApplyScheduling())
│
├── Program.cs                      — service registration + pipeline wiring
└── appsettings.json
```

---

## 🖥️ Built-in Admin UI

Provided by `SW.Scheduler.Viewer`. No extra code — just register and mount.

**URL:** `http://localhost:5000/scheduler-management`

**Pages:**
- **Dashboard** — running jobs count, recent executions, failure rate, success rate
- **Running** — live view of in-flight executions across all nodes
- **History** — filterable execution log (by job group and success/failure)
- **Detail** — per-execution breakdown including duration, error, and job parameters (context)

### How it is wired in `Program.cs`

```csharp
// Register — before builder.Build()
builder.Services.AddSchedulerMonitoring<AppDbContext>(); // must come first
builder.Services.AddSchedulerViewer(opts =>
{
    opts.Title      = "Sample App Scheduler";
    opts.PathPrefix = "/scheduler-management";

    // No auth guard here — open for development.
    // In production set AuthorizeAsync:
    // opts.AuthorizeAsync = ctx => Task.FromResult(ctx.User.IsInRole("Admin"));
});

// Mount — after builder.Build()
app.UseSchedulerViewer();  // auth guard middleware
app.MapSchedulerViewer();  // MVC routes under /scheduler-management
```

### Adding authentication

The viewer delegates auth entirely to your application. Set the `AuthorizeAsync` delegate to any logic that fits your stack:

```csharp
// ASP.NET Core Identity role
opts.AuthorizeAsync = ctx =>
    Task.FromResult(ctx.User.Identity?.IsAuthenticated == true
                 && ctx.User.IsInRole("Admin"));

// API key header
opts.AuthorizeAsync = ctx =>
{
    var key = ctx.Request.Headers["X-Scheduler-Key"].FirstOrDefault();
    return Task.FromResult(key == "my-secret");
};

// Cookie
opts.AuthorizeAsync = ctx =>
    Task.FromResult(ctx.Request.Cookies["scheduler_auth"] == "my-token");

// ASP.NET Core policy
opts.AuthorizeAsync = async ctx =>
{
    var svc = ctx.RequestServices.GetRequiredService<IAuthorizationService>();
    var result = await svc.AuthorizeAsync(ctx.User, "SchedulerAdminPolicy");
    return result.Succeeded;
};
```

> ⚠️ `AuthorizeAsync = null` (the default) allows **all** requests. Only use this in development.

---

## 🔌 REST API — Building a Custom / SPA UI

The REST controllers show how you would drive scheduling from your own frontend (React, Vue, Angular, mobile) or any HTTP client. They are a direct complement to the built-in viewer: the viewer reads, the API writes.

### `SchedulerController` — `POST/DELETE /api/scheduler/...`

Wraps `IScheduleRepository`. Every scheduling operation available in the library has a corresponding endpoint.

#### Simple jobs

| Method | Endpoint | Action |
|---|---|---|
| `GET` | `/api/scheduler/jobs` | List all discovered job types |
| `POST` | `/api/scheduler/count-customers/schedule` | Set cron for `CountCustomersJob` |
| `POST` | `/api/scheduler/count-customers/reschedule` | Change cron |
| `POST` | `/api/scheduler/count-customers/pause` | Pause trigger |
| `POST` | `/api/scheduler/count-customers/resume` | Resume trigger |
| `DELETE` | `/api/scheduler/count-customers` | Remove trigger |
| `POST` | `/api/scheduler/generate-report/schedule` | Set cron for `GenerateReportJob` |
| `POST` | `/api/scheduler/generate-report/reschedule` | Change cron |
| `POST` | `/api/scheduler/generate-report/pause` | Pause |
| `POST` | `/api/scheduler/generate-report/resume` | Resume |
| `DELETE` | `/api/scheduler/generate-report` | Remove |

#### Parameterized jobs

| Method | Endpoint | Action |
|---|---|---|
| `POST` | `/api/scheduler/send-emails/schedule` | Create a recurring email campaign |
| `POST` | `/api/scheduler/send-emails/schedule-once` | Run an email batch once |
| `POST` | `/api/scheduler/send-emails/{scheduleKey}/reschedule` | Change campaign cron |
| `POST` | `/api/scheduler/send-emails/{scheduleKey}/pause` | Pause campaign |
| `POST` | `/api/scheduler/send-emails/{scheduleKey}/resume` | Resume campaign |
| `DELETE` | `/api/scheduler/send-emails/{scheduleKey}` | Cancel campaign |
| `POST` | `/api/scheduler/notify-customer/schedule` | Schedule recurring notification |
| `POST` | `/api/scheduler/notify-customer/schedule-once` | Send one-off notification |
| `POST` | `/api/scheduler/notify-customer/{scheduleKey}/reschedule` | Change notification cron |
| `POST` | `/api/scheduler/notify-customer/{scheduleKey}/pause` | Pause |
| `POST` | `/api/scheduler/notify-customer/{scheduleKey}/resume` | Resume |
| `DELETE` | `/api/scheduler/notify-customer/{scheduleKey}` | Cancel |

#### Quick-start curl examples

```bash
# Schedule CountCustomersJob every 30 seconds
curl -X POST https://localhost:5001/api/scheduler/count-customers/schedule \
     -H "Content-Type: application/json" \
     -d '{"cronExpression":"0/30 * * * * ?"}'

# Create an email campaign that runs every day at 9 AM
curl -X POST https://localhost:5001/api/scheduler/send-emails/schedule \
     -H "Content-Type: application/json" \
     -d '{
           "scheduleKey": "acme-weekly",
           "cronExpression": "0 0 9 * * ?",
           "subject": "Weekly Update",
           "body": "Hello from ACME!",
           "filterByDomain": "acme.com"
         }'

# Send a one-off notification to customer #1 right now
curl -X POST https://localhost:5001/api/scheduler/notify-customer/schedule-once \
     -H "Content-Type: application/json" \
     -d '{
           "customerId": 1,
           "channel": "push",
           "message": "Your order has shipped!"
         }'

# Pause the campaign
curl -X POST https://localhost:5001/api/scheduler/send-emails/acme-weekly/pause

# Cancel the campaign
curl -X DELETE https://localhost:5001/api/scheduler/send-emails/acme-weekly
```

---

### `DashboardController` — `GET /api/dashboard/...`

Wraps `IScheduleReader`. Use these endpoints to populate your own dashboard or monitoring page.

| Method | Endpoint | Action |
|---|---|---|
| `GET` | `/api/dashboard/running` | All currently running executions |
| `GET` | `/api/dashboard/count-customers/last` | Last execution of `CountCustomersJob` |
| `GET` | `/api/dashboard/count-customers/recent?limit=20` | Recent executions |
| `GET` | `/api/dashboard/count-customers/failed?since=2026-01-01` | Failed executions since date |
| `GET` | `/api/dashboard/generate-report/last` | Last execution of `GenerateReportJob` |
| `GET` | `/api/dashboard/generate-report/recent` | Recent executions |
| `GET` | `/api/dashboard/generate-report/failed` | Failed executions |
| `GET` | `/api/dashboard/send-emails/{scheduleKey}/last` | Last execution of a campaign |
| `GET` | `/api/dashboard/send-emails/{scheduleKey}/recent` | Recent campaign executions |
| `GET` | `/api/dashboard/send-emails/{scheduleKey}/failed` | Failed campaign executions |
| `GET` | `/api/dashboard/notify-customer/{scheduleKey}/last` | Last notification execution |
| `GET` | `/api/dashboard/notify-customer/{scheduleKey}/recent` | Recent notification executions |
| `GET` | `/api/dashboard/notify-customer/{scheduleKey}/failed` | Failed notification executions |

---

## 🎯 Sample Jobs

### `CountCustomersJob` — simple job with retry

```csharp
[Schedule("0 * * * * ?", Description = "Count customers every minute")]
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.FireOnce)]
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 2)]
public class CountCustomersJob : IScheduledJob
{
    public async Task Execute() { /* counts customers, logs total */ }
}
```

- Starts automatically at startup via `[Schedule]`
- Can be overridden at runtime via `POST /api/scheduler/count-customers/schedule`
- Retries up to 3 times with a 2-minute gap on failure

### `GenerateReportJob` — simple job, no retry

```csharp
[Schedule("0 0 6 * * ?", Description = "Daily customer report at 6 AM")]
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.Skip)]
public class GenerateReportJob : IScheduledJob
{
    public async Task Execute() { /* logs all customer names */ }
}
```

- Starts at 6 AM daily; misfired triggers are skipped (no catch-up)
- No `[RetryConfig]` — a failed report is simply logged and dropped

### `SendCustomerEmailsJob` — parameterized, concurrent campaigns

```csharp
[ScheduleConfig(AllowConcurrentExecution = true, MisfireInstructions = MisfireInstructions.Skip)]
[RetryConfig(MaxRetries = 5, RetryAfterMinutes = 10)]
public class SendCustomerEmailsJob : IScheduledJob<SendEmailsParams>
{
    public async Task Execute(SendEmailsParams p) { /* sends emails */ }
}

public record SendEmailsParams(string Subject, string Body, string? FilterByDomain = null);
```

- Runtime-only — no `[Schedule]` attribute
- Multiple campaigns (different `scheduleKey` values) run independently and concurrently
- Each `scheduleKey` is a separate Quartz job with its own data map and retry counter

### `NotifyCustomerJob` — parameterized, per-customer locking

```csharp
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.Skip)]
public class NotifyCustomerJob : IScheduledJob<NotifyCustomerParams>
{
    public async Task Execute(NotifyCustomerParams p) { /* sends SMS/push/webhook */ }
}

public record NotifyCustomerParams(int CustomerId, string Channel, string Message);
```

- Runtime-only
- `AllowConcurrentExecution = false` per schedule key ensures the same customer isn't notified in parallel by the same recurring schedule

---

## ⚙️ Switching to a Persistent Store

The sample uses an in-memory store by default. To use PostgreSQL, replace the `AddScheduler` block in `Program.cs`:

```csharp
// Before (in-memory):
builder.Services.AddScheduler(options => { ... }, typeof(Program).Assembly);

// After (PostgreSQL):
builder.Services.AddPgSqlScheduler(
    connectionString: builder.Configuration.GetConnectionString("Postgres")!,
    schema: "quartz",
    configureOptions: options => { ... },
    assemblies: typeof(Program).Assembly);
```

And update `AppDbContext.OnModelCreating`:

```csharp
// Before:
modelBuilder.ApplyScheduling();

// After:
modelBuilder.UseQuartzPostgreSql("quartz");
```

Then run migrations:

```bash
dotnet ef migrations add AddScheduler
dotnet ef database update
```

The same pattern applies for SQL Server (`UseQuartzSqlServer`) and MySQL (`UseQuartzMySql`).

