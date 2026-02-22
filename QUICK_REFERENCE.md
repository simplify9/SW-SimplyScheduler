# SW.Scheduler - Quick Reference

## 📋 At a Glance

| Feature | `IScheduledJob` | `IScheduledJob<TParam>` |
|---------|----------------|------------------------|
| **Declarative Scheduling** | ✅ Use `[Schedule]` | ❌ Not supported |
| **Runtime Scheduling** | ✅ Supported | ✅ Supported |
| **Parameters** | ❌ No params | ✅ Per-schedule params |

---

## 🚀 Quick Start

### 1. Define a Job

**Simple Job:**
```csharp
[Schedule("0 * * * * ?", TriggerKey = "my-job")]
public class MyJob : IScheduledJob
{
    public Task Execute()
    {
        // Your logic here
        return Task.CompletedTask;
    }
}
```

**Parameterized Job:**
```csharp
public class MyParamJob : IScheduledJob<MyParams>
{
    public Task Execute(MyParams jobParams)
    {
        // Your logic with params
        return Task.CompletedTask;
    }
}
```

### 2. Schedule at Runtime

```csharp
// Inject IScheduleRepository
private readonly IScheduleRepository _scheduler;

// Schedule simple job
await _scheduler.Schedule<MyJob>("0 0 9 * * ?");

// Schedule with params
await _scheduler.Schedule<MyParamJob, MyParams>(
    new MyParams { Value = "test" },
    "0 0 9 * * ?"
);
```

---

## 🎯 Common Use Cases

### ✅ Fixed Schedule (Declarative)
```csharp
[Schedule("0 0 2 * * ?", Description = "Daily backup at 2 AM")]
public class BackupJob : IScheduledJob { }
```

### ✅ User-Defined Schedule (Runtime)
```csharp
[HttpPost("schedule")]
public async Task<string> Schedule(string cron)
{
    return await _scheduler.Schedule<ReportJob>(cron);
}
```

### ✅ Different Params, Same Job
```csharp
// VIP customers at 9 AM
await _scheduler.Schedule<EmailJob, EmailParams>(
    new EmailParams { Group = "VIP" },
    "0 0 9 * * ?",
    "vip-emails"
);

// Regular customers at 10 AM
await _scheduler.Schedule<EmailJob, EmailParams>(
    new EmailParams { Group = "Regular" },
    "0 0 10 * * ?",
    "regular-emails"
);
```

### ✅ Run Once
```csharp
// Run in 5 minutes
await _scheduler.ScheduleOnce<MyJob>(
    DateTime.UtcNow.AddMinutes(5)
);
```

### ✅ Trigger Immediately
```csharp
// Execute now (doesn't create schedule)
await _scheduler.TriggerJobNow<MyJob>();
```

---

## 🎛️ Management Operations

```csharp
// Reschedule
await _scheduler.RescheduleJob("my-trigger", "0 0 12 * * ?");

// Pause
await _scheduler.PauseJob("my-trigger");

// Resume
await _scheduler.ResumeJob("my-trigger");

// Remove
await _scheduler.UnscheduleJob("my-trigger");

// List all jobs
var jobs = _scheduler.GetJobDefinitions();
```

---

## 📅 Cron Cheat Sheet

| Expression | Meaning |
|------------|---------|
| `0 * * * * ?` | Every minute |
| `0 0 * * * ?` | Every hour |
| `0 0 9 * * ?` | Daily at 9 AM |
| `0 0 9 * * MON-FRI` | Weekdays at 9 AM |
| `0 */15 * * * ?` | Every 15 minutes |
| `0 0 2 * * ?` | Daily at 2 AM |
| `0 0 0 1 * ?` | First of month |
| `0 0 0 ? * SUN` | Sundays at midnight |

**Format:** `second minute hour dayOfMonth month dayOfWeek`

---

## ⚠️ Important Notes

1. **Trigger Keys**: Must be unique. Use descriptive names.
2. **Parameters**: Must be JSON-serializable (simple types, POCOs).
3. **Concurrency**: Jobs don't run concurrently by default.
4. **Persistence**: Schedules survive app restarts if using DB persistence.
5. **Attribute Limitation**: `[Schedule]` only works on `IScheduledJob` (not `IScheduledJob<T>`).

---

## 🔧 Setup

```csharp
// Program.cs
builder.Services.AddScheduler(typeof(Program).Assembly);
```

---

## 💡 Tips

- Use `[Schedule]` for **fixed** schedules that don't change
- Use runtime API for **dynamic** schedules controlled by users
- Override declarative schedules by unscheduling + rescheduling
- Use meaningful trigger keys for management
- Test cron expressions at https://crontab.guru (adjust for Quartz format)

---

## 🆘 Troubleshooting

**Job not running?**
- Check cron expression is valid
- Verify job is registered: `GetJobDefinitions()`
- Check logs for errors

**Trigger already exists?**
- Use unique trigger keys
- Or unschedule first
- Or use `RescheduleJob`

**Params not working?**
- Ensure type is serializable
- Check param is not null
- Verify generic type matches

---

## 📚 Full Documentation

See [SCHEDULER_GUIDE.md](SCHEDULER_GUIDE.md) for complete documentation.
