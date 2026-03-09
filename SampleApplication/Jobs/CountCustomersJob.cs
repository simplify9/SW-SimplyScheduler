using Microsoft.EntityFrameworkCore;
using SampleApplication.Data;
using SW.Scheduler;

namespace SampleApplication.Jobs;

/// <summary>
/// Counts all customers and logs the total.
/// Runs every minute by default via the [Schedule] attribute.
/// Concurrent executions are disallowed and misfired triggers fire once.
/// </summary>
[Schedule("0 * * * * ?", Description = "Count customers every minute")]
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.FireOnce)]
[RetryConfig(MaxRetries = 3, RetryAfterMinutes = 2)]
public class CountCustomersJob : IScheduledJob
{
    private readonly AppDbContext _db;
    public CountCustomersJob(AppDbContext db) => _db = db;


    public async Task Execute()
    {
        var total = await _db.Customers.CountAsync();
        Console.WriteLine($"[CountCustomersJob] Total customers: {total} @ {DateTimeOffset.UtcNow:O}");
    }
}
