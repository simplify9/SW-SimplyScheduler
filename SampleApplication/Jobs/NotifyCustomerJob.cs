using SampleApplication.Data;
using SW.Scheduler;

namespace SampleApplication.Jobs;

/// <summary>
/// Parameters for NotifyCustomerJob.
/// Targets a single customer by id with a notification message.
/// </summary>
public record NotifyCustomerParams(
    int CustomerId,
    string Channel,     // "sms" | "push" | "webhook"
    string Message
);

/// <summary>
/// Sends a notification to a single customer via the specified channel.
/// Parameterized — must be scheduled at runtime via IScheduleRepository.
/// Disallows concurrent execution per schedule key so the same customer
/// is not notified in parallel by the same recurring schedule.
/// Misfired executions are skipped (don't pile up).
/// </summary>
[ScheduleConfig(AllowConcurrentExecution = false, MisfireInstructions = MisfireInstructions.Skip)]
public class NotifyCustomerJob : IScheduledJob<NotifyCustomerParams>
{
    private readonly AppDbContext _db;
    public NotifyCustomerJob(AppDbContext db) => _db = db;

    public async Task Execute(NotifyCustomerParams jobParams)
    {
        var customer = await _db.Customers.FindAsync(jobParams.CustomerId);
        if (customer == null)
        {
            Console.WriteLine($"[NotifyCustomerJob] Customer {jobParams.CustomerId} not found — skipping.");
            return;
        }

        Console.WriteLine(
            $"[NotifyCustomerJob] [{jobParams.Channel.ToUpper()}] → {customer.Name} ({customer.Email}): " +
            $"'{jobParams.Message}' @ {DateTimeOffset.UtcNow:O}");
    }
}

