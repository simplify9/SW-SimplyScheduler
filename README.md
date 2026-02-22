# SW.Scheduler

A simple, type-safe wrapper around Quartz.NET for .NET 8+ applications that makes job scheduling intuitive and developer-friendly.

## 🌟 Features

- ✅ **Declarative Scheduling** - Use `[Schedule]` attribute for simple jobs
- ✅ **Runtime Scheduling API** - Dynamic scheduling with full control
- ✅ **Type-Safe** - Compile-time checking with generics
- ✅ **Parameterized Jobs** - Each schedule can have different parameters
- ✅ **Full Job Management** - Pause, resume, reschedule, unschedule
- ✅ **Persistent Storage** - Works with any Quartz persistence backend
- ✅ **Dependency Injection** - First-class DI support
- ✅ **Clean Architecture** - Wrapper pattern keeps complexity hidden

## 🚀 Quick Start

### Installation

SW.Scheduler uses a **two-package architecture** for clean separation:

```bash
# In your API/job definition project (lightweight, no dependencies)
dotnet add package SW.Scheduler.Sdk

# In your host/startup project (full scheduler implementation)
dotnet add package SW.Scheduler
```

**Why two packages?**
- **SW.Scheduler.Sdk**: Lightweight interfaces only - reference this in projects that define jobs
- **SW.Scheduler**: Full implementation with Quartz.NET - reference this only in your startup/host project

### 1. Define Jobs (in API project with SDK reference)

```csharp
using SW.PrimitiveTypes;

// This project only needs SW.Scheduler.Sdk
[Schedule("0 0 2 * * ?", TriggerKey = "backup-daily")]
public class BackupJob : IScheduledJob
{
    private readonly ILogger<BackupJob> _logger;
    
    public BackupJob(ILogger<BackupJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("Running backup...");
        // Your backup logic here
    }
}
```

### 2. Register the Scheduler (in host project)

```csharp
// Program.cs (host project needs SW.Scheduler reference)
using SW.Scheduler;

var builder = WebApplication.CreateBuilder(args);

// Register scheduler (scans for jobs in specified assemblies)
builder.Services.AddScheduler(
    typeof(BackupJob).Assembly  // Assembly containing your jobs
);

var app = builder.Build();
app.Run();
```

That's it! Jobs with `[Schedule]` attribute will automatically run.

## 📖 Documentation

- **[Complete Guide](SCHEDULER_GUIDE.md)** - Full documentation with examples
- **[Quick Reference](QUICK_REFERENCE.md)** - Cheat sheet for common tasks
- **[Implementation Summary](IMPLEMENTATION_SUMMARY.md)** - Architecture details

## 🎯 Usage Examples

### Declarative Scheduling (Simple Jobs)

```csharp
[Schedule("0 0 2 * * ?", Description = "Daily cleanup at 2 AM")]
public class CleanupJob : IScheduledJob
{
    public Task Execute()
    {
        // Cleanup logic
        return Task.CompletedTask;
    }
}
```

### Runtime Scheduling (Dynamic)

```csharp
public class SchedulerService
{
    private readonly IScheduleRepository _scheduler;
    
    public SchedulerService(IScheduleRepository scheduler)
    {
        _scheduler = scheduler;
    }
    
    public async Task ScheduleUserReport(string cronExpression)
    {
        var triggerKey = await _scheduler.Schedule<ReportJob>(
            cronExpression: cronExpression,
            triggerKey: "user-report"
        );
        
        Console.WriteLine($"Scheduled with trigger: {triggerKey}");
    }
}
```

### Parameterized Jobs

```csharp
public class EmailParams
{
    public string Template { get; set; }
    public string Recipient { get; set; }
}

public class EmailJob : IScheduledJob<EmailParams>
{
    public Task Execute(EmailParams param)
    {
        // Send email with template to recipient
        return Task.CompletedTask;
    }
}

// Schedule different instances
await _scheduler.Schedule<EmailJob, EmailParams>(
    param: new EmailParams { Template = "Welcome", Recipient = "user@example.com" },
    cronExpression: "0 0 9 * * ?",
    triggerKey: "welcome-email"
);
```

### Job Management

```csharp
// Reschedule
await _scheduler.RescheduleJob("my-trigger", "0 0 12 * * ?");

// Pause
await _scheduler.PauseJob("my-trigger");

// Resume
await _scheduler.ResumeJob("my-trigger");

// Remove
await _scheduler.UnscheduleJob("my-trigger");

// Trigger immediately
await _scheduler.TriggerJobNow<MyJob>();
```

## 🏗️ Architecture

### Job Types

| Interface | Use Case | Declarative | Runtime | Parameters |
|-----------|----------|------------|---------|------------|
| `IScheduledJob` | Simple recurring tasks | ✅ `[Schedule]` | ✅ API | ❌ No |
| `IScheduledJob<T>` | Parameterized tasks | ❌ No | ✅ API | ✅ Yes |

### Core Components

- **`IScheduleRepository`** - Public API for runtime scheduling
- **`ScheduleAttribute`** - Declarative scheduling for simple jobs
- **`SchedulerPreparation`** - Background service that registers jobs on startup
- **`QuartzBackgroundJob`** - Internal executor (you never interact with this)

## 📅 Cron Expression Format

Quartz uses: `second minute hour dayOfMonth month dayOfWeek`

Common patterns:
```
0 * * * * ?         → Every minute
0 0 * * * ?         → Every hour
0 0 9 * * ?         → Daily at 9 AM
0 0 9 * * MON-FRI   → Weekdays at 9 AM
0 */15 * * * ?      → Every 15 minutes
0 0 0 1 * ?         → First of month at midnight
```

## 🔧 Configuration

### With Database Persistence

```csharp
builder.Services.AddScheduler(typeof(Program).Assembly);

builder.Services.AddQuartz(q =>
{
    q.UsePersistentStore(store =>
    {
        // PostgreSQL
        store.UsePostgres(connectionString);
        
        // Or SQL Server
        // store.UseSqlServer(connectionString);
        
        store.UseJsonSerializer();
    });
});
```

### In-Memory (Development)

```csharp
// Just AddScheduler - no additional configuration needed
builder.Services.AddScheduler(typeof(Program).Assembly);
```

## 🧪 Sample Application

The repository includes a complete sample application demonstrating:
- Declarative scheduling with `[Schedule]`
- Runtime scheduling via API
- Job management endpoints
- Database persistence with EF Core

Run it:
```bash
cd SampleApplication
dotnet run
```

Visit: `https://localhost:5001/swagger`

## 🎓 Best Practices

### ✅ DO

- Use `[Schedule]` for fixed, predictable schedules
- Use runtime API for user-configurable schedules
- Keep jobs lightweight and focused
- Handle errors gracefully in Execute methods
- Use meaningful trigger keys
- Inject dependencies via constructor

### ❌ DON'T

- Don't use `[Schedule]` on `IScheduledJob<T>` (not supported)
- Don't perform long-running operations without progress tracking
- Don't forget to validate cron expressions from user input
- Don't use complex object graphs as parameters

## 🤝 Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## 📄 License

[Your License Here]

## 🆘 Support

- **Documentation**: See [SCHEDULER_GUIDE.md](SCHEDULER_GUIDE.md)
- **Issues**: [GitHub Issues](https://github.com/yourrepo/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourrepo/discussions)

## 🗺️ Roadmap

- [ ] Job execution history and monitoring
- [ ] Health checks integration
- [ ] Metrics and telemetry
- [ ] Job chaining support
- [ ] Advanced retry policies
- [ ] Job priority support
- [ ] Time zone support for schedules

## 📦 Package Structure

```
SW.Scheduler.Sdk/           → Interfaces and contracts (lightweight, no dependencies)
SW.Scheduler/               → Core scheduler implementation
SW.Scheduler.EfCore/        → EF Core entities for Quartz tables
SW.Scheduler.PgSql/         → PostgreSQL-specific configurations
SampleApplication/          → Example implementation
```

## 🔍 Troubleshooting

### Job not executing?
1. Verify cron expression: `CronExpression.IsValidExpression(cronExpr)`
2. Check logs for errors
3. Confirm job is registered: `_scheduler.GetJobDefinitions()`

### Trigger already exists?
Use unique trigger keys or unschedule first:
```csharp
await _scheduler.UnscheduleJob("my-trigger");
await _scheduler.Schedule<MyJob>(cronExpr, "my-trigger");
```

### Parameters not working?
- Ensure parameter type is JSON-serializable
- Check parameter is not null when scheduling
- Verify generic type parameter matches job definition

## 📊 Example Project Structure

```
YourSolution/
├── YourApi/                       (References: SW.Scheduler.Sdk)
│   ├── Jobs/
│   │   ├── BackupJob.cs          → [Schedule("...")]
│   │   ├── ReportJob.cs           → [Schedule("...")]
│   │   └── EmailJob.cs            → IScheduledJob<EmailParams>
│   └── Controllers/
│       └── SchedulerController.cs → Uses IScheduleRepository
│
└── YourHost/                      (References: SW.Scheduler)
    └── Program.cs                 → AddScheduler(typeof(BackupJob).Assembly)
```

## 📦 Package Architecture

### SW.Scheduler.Sdk (Lightweight Interfaces)
- **Size**: < 50 KB
- **Dependencies**: None
- **Contains**: Interfaces, attributes, contracts
- **Reference in**: Job definition projects, API projects

### SW.Scheduler (Full Implementation)
- **Dependencies**: Quartz.NET, EF Core
- **Contains**: Scheduler engine, job execution, persistence
- **Reference in**: Host/startup projects only

**Benefits:**
- ✅ Job definition projects stay lightweight
- ✅ No unnecessary dependencies in API projects
- ✅ Clean separation of concerns
- ✅ Faster compile times for job projects

---

Made with ❤️ for the .NET community
