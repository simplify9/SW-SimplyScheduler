using Microsoft.EntityFrameworkCore;
using SampleApplication.Data;
using SW.Scheduler;

namespace SampleApplication.Jobs;

/// <summary>
/// Generates a daily summary report of all customers.
///
/// Demonstrates:
///   - [Schedule] attribute for startup cron registration (simple job)
///   - MisfireInstructions.Skip — if the server was down at trigger time, skip it
///   - No [RetryConfig] — report generation failures are logged and skipped
///   - [ScheduleConfig] defaults (AllowConcurrentExecution = false)
///   - Runtime override via IScheduleRepository.Schedule&lt;GenerateReportJob&gt;(newCron)
/// </summary>
[Schedule("0 0 6 * * ?", Description = "Daily customer report at 6 AM")]
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.Skip)]
public class GenerateReportJob : IScheduledJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<GenerateReportJob> _logger;

    public GenerateReportJob(AppDbContext db, ILogger<GenerateReportJob> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task Execute()
    {
        var customers = await _db.Customers.AsNoTracking().ToListAsync();

        _logger.LogInformation(
            "[GenerateReportJob] Report generated at {Time}: {Count} customer(s) total.",
            DateTimeOffset.UtcNow, customers.Count);

        foreach (var c in customers)
            _logger.LogInformation("  - {Name} <{Email}>", c.Name, c.Email);
    }
}

