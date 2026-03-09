# SW.Scheduler - Complete Implementation Guide

## Overview

SW.Scheduler is a wrapper around Quartz.NET that provides a simple, type-safe way to schedule jobs in .NET applications. It supports both declarative scheduling (via attributes) and runtime scheduling (via API).

## Architecture

### Job Types

1. **`IScheduledJob`** - Simple jobs without parameters
   - Use `[Schedule]` attribute for declarative scheduling
   - Can be scheduled/rescheduled at runtime via `IScheduleRepository`
   
2. **`IScheduledJob<TParam>`** - Parameterized jobs
   - **Runtime-only scheduling** - no attribute support
   - Each schedule instance can have different parameters
   - Parameters must be JSON-serializable

### Core Components

- **`QuartzBackgroundJob`** - Internal wrapper that executes all scheduled jobs
- **`SchedulerPreparation`** - Background service that:
  - Registers all jobs as durable jobs in Quartz
  - Auto-schedules `IScheduledJob` implementations with `[Schedule]` attribute
- **`JobsDiscovery`** - Discovers and catalogs all job implementations
- **`IScheduleRepository`** - Runtime API for scheduling, rescheduling, and managing jobs

## Usage

### 1. Simple Job with Declarative Scheduling

```csharp
using SW.PrimitiveTypes;

[Schedule("0 * * * * ?", 
    TriggerKey = "count-customers-every-minute", 
    Description = "Count customers every minute")]
public class CountCustomersJob : IScheduledJob
{
    private readonly AppDbContext _db;
    
    public CountCustomersJob(AppDbContext db) => _db = db;

    public async Task Execute()
    {
        var total = await _db.Customers.CountAsync();
        Console.WriteLine($"Total customers: {total}");
    }
}
```

**Features:**
- Automatically scheduled on application startup
- Cron expression: `"0 * * * * ?"` = every minute at second 0
- Optional `TriggerKey` for runtime management
- Can be overridden/rescheduled at runtime

### 2. Simple Job with Runtime Scheduling

```csharp
public class BackupJob : IScheduledJob
{
    public async Task Execute()
    {
        // Backup logic
    }
}

// Schedule at runtime
public class BackupController : ControllerBase
{
    private readonly IScheduleRepository _scheduler;
    
    [HttpPost("schedule-backup")]
    public async Task<IActionResult> ScheduleBackup(string cronExpression)
    {
        var triggerKey = await _scheduler.Schedule<BackupJob>(
            cronExpression: cronExpression,
            triggerKey: "daily-backup"
        );
        
        return Ok(new { triggerKey });
    }
}
```

### 3. Parameterized Job (Runtime Only)

```csharp
public class CustomerEmailParams
{
    public string CustomerGroup { get; set; }
    public string EmailTemplate { get; set; }
}

public class SendCustomerEmailsJob : IScheduledJob<CustomerEmailParams>
{
    private readonly IEmailService _emailService;
    
    public SendCustomerEmailsJob(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Execute(CustomerEmailParams jobParams)
    {
        // Send emails to specific customer group
        await _emailService.SendToGroup(
            jobParams.CustomerGroup, 
            jobParams.EmailTemplate
        );
    }
}

// Schedule different instances with different parameters
public class EmailController : ControllerBase
{
    private readonly IScheduleRepository _scheduler;
    
    [HttpPost("schedule-vip-emails")]
    public async Task<IActionResult> ScheduleVipEmails()
    {
        var triggerKey = await _scheduler.Schedule<SendCustomerEmailsJob, CustomerEmailParams>(
            param: new CustomerEmailParams 
            { 
                CustomerGroup = "VIP", 
                EmailTemplate = "VipNewsletter" 
            },
            cronExpression: "0 0 9 * * ?", // Daily at 9 AM
            triggerKey: "vip-newsletter-daily"
        );
        
        return Ok(new { triggerKey });
    }
    
    [HttpPost("schedule-regular-emails")]
    public async Task<IActionResult> ScheduleRegularEmails()
    {
        var triggerKey = await _scheduler.Schedule<SendCustomerEmailsJob, CustomerEmailParams>(
            param: new CustomerEmailParams 
            { 
                CustomerGroup = "Regular", 
                EmailTemplate = "RegularNewsletter" 
            },
            cronExpression: "0 0 10 * * ?", // Daily at 10 AM
            triggerKey: "regular-newsletter-daily"
        );
        
        return Ok(new { triggerKey });
    }
}
```

## IScheduleRepository API

### Schedule with Cron Expression

```csharp
// Simple job
Task<string> Schedule<TScheduler>(
    string cronExpression, 
    string? triggerKey = null
) where TScheduler : IScheduledJob;

// Parameterized job
Task<string> Schedule<TScheduler, TParam>(
    TParam param, 
    string cronExpression, 
    string? triggerKey = null
) where TScheduler : IScheduledJob<TParam>;
```

### One-Time Execution

```csharp
// Simple job - run once
Task<string> ScheduleOnce<TScheduler>(
    DateTime? runAt = null, 
    string? triggerKey = null
) where TScheduler : IScheduledJob;

// Parameterized job - run once
Task<string> ScheduleOnce<TScheduler, TParam>(
    TParam param, 
    DateTime? runAt = null, 
    string? triggerKey = null
) where TScheduler : IScheduledJob<TParam>;
```

### Job Management

```csharp
// Reschedule with new cron expression
Task RescheduleJob(string triggerKey, string newCronExpression);

// Remove scheduled job
Task UnscheduleJob(string triggerKey);

// Pause job execution
Task PauseJob(string triggerKey);

// Resume paused job
Task ResumeJob(string triggerKey);

// Trigger immediately (doesn't create a schedule)
Task TriggerJobNow<TScheduler>() where TScheduler : IScheduledJob;
Task TriggerJobNow<TScheduler, TParam>(TParam param) 
    where TScheduler : IScheduledJob<TParam>;

// Get all registered jobs
IEnumerable<IScheduledJobDefinition> GetJobDefinitions();
```

## Cron Expression Examples

```
"0 * * * * ?"       = Every minute at second 0
"0 0 * * * ?"       = Every hour at minute 0
"0 0 9 * * ?"       = Every day at 9:00 AM
"0 0 9 * * MON-FRI" = Weekdays at 9:00 AM
"0 */15 * * * ?"    = Every 15 minutes
"0 0 0 1 * ?"       = First day of every month at midnight
```

## Setup

### 1. Install Package (when published)

```bash
dotnet add package SW.Scheduler
```

### 2. Register Scheduler in Program.cs

```csharp
using SW.Scheduler;

var builder = WebApplication.CreateBuilder(args);

// Register scheduler with assemblies to scan
builder.Services.AddScheduler(
    typeof(Program).Assembly  // Scans for jobs in current assembly
);

// Configure Quartz with database persistence (optional)
builder.Services.AddQuartz(q =>
{
    q.UsePersistentStore(store =>
    {
        store.UsePostgres(connectionString);
        store.UseJsonSerializer();
    });
});

var app = builder.Build();
app.Run();
```

## Best Practices

### 1. Job Design
- Keep jobs lightweight and focused
- Use dependency injection for services
- Handle errors gracefully inside Execute method
- Use logging to track execution

### 2. Scheduling Strategy
- Use `[Schedule]` for predictable, startup-defined schedules
- Use runtime API for dynamic, user-configured schedules
- Use meaningful `triggerKey` values for management

### 3. Parameters
- Keep parameter objects simple and serializable
- Avoid complex object graphs
- Document expected parameter structure

### 4. Error Handling

```csharp
public class RobustJob : IScheduledJob
{
    private readonly ILogger<RobustJob> _logger;
    
    public RobustJob(ILogger<RobustJob> logger) => _logger = logger;

    public async Task Execute()
    {
        try
        {
            // Job logic
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job execution failed");
            // Optionally rethrow to trigger Quartz retry logic
            throw;
        }
    }
}
```

## Advanced: Overriding Declarative Schedules

```csharp
// Job defined with default schedule
[Schedule("0 0 * * * ?", TriggerKey = "default-report")]
public class ReportJob : IScheduledJob
{
    public Task Execute() { /* ... */ }
}

// Override at runtime
public async Task ChangeSchedule(IScheduleRepository scheduler)
{
    // Unschedule the default trigger
    await scheduler.UnscheduleJob("default-report");
    
    // Create new schedule
    await scheduler.Schedule<ReportJob>(
        cronExpression: "0 0 0 * * ?",  // Change to midnight
        triggerKey: "custom-report"
    );
}
```

## Implementation Details

### Job Identity
- Each job is registered as a **durable job** in Quartz
- Identity: `JobKey(JobType.Name, JobType.Namespace)`
- Multiple triggers can reference the same job

### Parameter Serialization
- Parameters are serialized to JSON using `System.Text.Json`
- Stored in trigger's `JobDataMap` with key `"JobParams"`
- Deserialized before passing to `Execute(TParam)` method

### Concurrency
- All jobs use `[DisallowConcurrentExecution]`
- Same job won't run concurrently even with multiple triggers
- Use different job classes if you need parallel execution

### Persistence
- Jobs and triggers are persisted if Quartz is configured with database
- Survives application restarts
- Declarative schedules (via `[Schedule]`) are recreated on startup if missing

## Troubleshooting

### Job Not Executing
1. Check if job is registered: `_scheduler.GetJobDefinitions()`
2. Verify cron expression is valid
3. Check application logs for errors
4. Ensure job's dependencies are registered in DI

### Parameter Not Received
1. Verify parameter type is serializable
2. Check parameter is not null when scheduling
3. Ensure `TParam` matches the job's generic parameter

### Trigger Already Exists
- Use unique trigger keys
- Or unschedule existing trigger first
- Or use `RescheduleJob` to update existing trigger

## Migration from Old Approach

If you had jobs with `DefaultSchedule()` method:

**Before:**
```csharp
public class MyJob : IScheduledJob
{
    public string DefaultSchedule() => "0 * * * * ?";
    public Task Execute() { /* ... */ }
}
```

**After:**
```csharp
[Schedule("0 * * * * ?")]
public class MyJob : IScheduledJob
{
    public Task Execute() { /* ... */ }
}
```

## Summary

✅ **IScheduledJob** → Use `[Schedule]` attribute + runtime API  
✅ **IScheduledJob<TParam>** → Runtime API only  
✅ Type-safe scheduling with compile-time checks  
✅ Flexible: declarative OR runtime OR both  
✅ Full job lifecycle management  
✅ Works with any Quartz persistence backend  
