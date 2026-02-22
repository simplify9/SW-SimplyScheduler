# SW.Scheduler.Sdk

The SDK package for SW.Scheduler containing all public interfaces and contracts. This is a lightweight, dependency-free package that should be referenced by projects that define scheduled jobs.

## 📦 Package Purpose

This package contains:
- `IScheduledJob` - Interface for simple scheduled jobs
- `IScheduledJob<TParam>` - Interface for parameterized scheduled jobs
- `IScheduleRepository` - Interface for runtime job scheduling and management
- `ScheduleAttribute` - Attribute for declarative job scheduling
- `IScheduledJobDefinition` - Job metadata interface

## 🎯 When to Use This Package

### ✅ Use SW.Scheduler.Sdk in:
- **API/Web Projects** that define jobs
- **Class Libraries** that contain job definitions
- **Any project** that needs to use `IScheduleRepository` for scheduling

### ✅ Use SW.Scheduler in:
- **Host/Startup Projects** that run the scheduler
- **Projects** that configure Quartz and start the scheduling engine

## 📥 Installation

```bash
# In your API/job definition project
dotnet add package SW.Scheduler.Sdk

# In your host/startup project
dotnet add package SW.Scheduler
```

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────┐
│  Your API Project (Job Definitions)            │
│  └─ References: SW.Scheduler.Sdk (lightweight) │
│                                                 │
│  [Schedule("...")]                              │
│  public class MyJob : IScheduledJob { }         │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  Your Host Project (Startup/Configuration)     │
│  └─ References: SW.Scheduler (full library)    │
│                                                 │
│  builder.Services.AddScheduler(...);            │
└─────────────────────────────────────────────────┘
```

## 🚀 Example Usage

### In Your Job Definition Project

```csharp
using SW.PrimitiveTypes;

// Reference: SW.Scheduler.Sdk only
[Schedule("0 0 2 * * ?")]
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
        // Backup logic
    }
}
```

### In Your Controller/Service (Runtime Scheduling)

```csharp
using SW.PrimitiveTypes;

// Reference: SW.Scheduler.Sdk only
public class SchedulerService
{
    private readonly IScheduleRepository _scheduler;
    
    public SchedulerService(IScheduleRepository scheduler)
    {
        _scheduler = scheduler;
    }
    
    public async Task<string> ScheduleReport(string cronExpression)
    {
        return await _scheduler.Schedule<ReportJob>(
            cronExpression: cronExpression,
            triggerKey: "user-report"
        );
    }
}
```

### In Your Host/Startup Project

```csharp
// Reference: SW.Scheduler (includes SDK transitively)
using SW.Scheduler;

var builder = WebApplication.CreateBuilder(args);

// Register scheduler - scans assemblies for jobs
builder.Services.AddScheduler(
    typeof(BackupJob).Assembly  // Assembly containing jobs
);

var app = builder.Build();
app.Run();
```

## 📋 API Reference

### IScheduledJob
Interface for simple scheduled jobs without parameters.

```csharp
public interface IScheduledJob : IScheduledJobBase
{
    Task Execute();
}
```

### IScheduledJob<TParam>
Interface for parameterized scheduled jobs.

```csharp
public interface IScheduledJob<TParam> : IScheduledJobWithParams
{
    Task Execute(TParam jobParams);
}
```

### IScheduleRepository
Interface for runtime job scheduling and management.

```csharp
public interface IScheduleRepository
{
    // Scheduling
    Task<string> Schedule<TScheduler>(string cronExpression, string? triggerKey = null);
    Task<string> Schedule<TScheduler, TParam>(TParam param, string cronExpression, string? triggerKey = null);
    Task<string> ScheduleOnce<TScheduler>(DateTime? runAt = null, string? triggerKey = null);
    Task<string> ScheduleOnce<TScheduler, TParam>(TParam param, DateTime? runAt = null, string? triggerKey = null);
    
    // Management
    Task RescheduleJob(string triggerKey, string newCronExpression);
    Task UnscheduleJob(string triggerKey);
    Task PauseJob(string triggerKey);
    Task ResumeJob(string triggerKey);
    
    // Query
    IEnumerable<IScheduledJobDefinition> GetJobDefinitions();
    
    // Immediate execution
    Task TriggerJobNow<TScheduler>();
    Task TriggerJobNow<TScheduler, TParam>(TParam param);
}
```

### ScheduleAttribute
Attribute for declarative scheduling (only works with `IScheduledJob`).

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ScheduleAttribute : Attribute
{
    public string CronExpression { get; }
    public string? TriggerKey { get; set; }
    public string? Description { get; set; }
    
    public ScheduleAttribute(string cronExpression);
}
```

## 🔗 Dependencies

**None!** This is a pure interface/contract package with no external dependencies.

## 📦 NuGet Package Info

- **Package ID**: `SW.Scheduler.Sdk`
- **Target Framework**: .NET 8.0
- **Dependencies**: None
- **Size**: < 50 KB

## 🎯 Benefits of Two-Package Approach

### For Job Definition Projects
✅ Lightweight reference (no Quartz or heavy dependencies)  
✅ Fast compile times  
✅ Clean separation of concerns  
✅ Easy to share job definitions across projects  

### For Host Projects
✅ Full scheduler implementation  
✅ Quartz.NET integration  
✅ Database persistence support  
✅ Complete job execution engine  

## 📚 Documentation

For complete documentation on using the scheduler, see:
- [Main README](../README.md)
- [Scheduler Guide](../SCHEDULER_GUIDE.md)
- [Quick Reference](../QUICK_REFERENCE.md)

## 🆘 Support

- **Issues**: Report on the main SW.Scheduler repository
- **Documentation**: See parent project documentation

## 📄 License

Same as SW.Scheduler parent project.
