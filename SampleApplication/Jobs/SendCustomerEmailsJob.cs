using Microsoft.EntityFrameworkCore;
using SampleApplication.Data;
using SW.Scheduler;

namespace SampleApplication.Jobs;

/// <summary>
/// Parameters for SendCustomerEmailsJob.
/// Controls which customers to target and what subject to use.
/// </summary>
public record SendEmailsParams(
    string Subject,
    string Body,
    string? FilterByDomain = null   // optional: only email customers with this domain
);

/// <summary>
/// Sends emails to customers based on the provided parameters.
/// Parameterized — must be scheduled at runtime via IScheduleRepository.
/// Allows concurrent execution so multiple campaigns can run simultaneously.
/// </summary>
[ScheduleConfig(AllowConcurrentExecution = true, MisfireInstructions = MisfireInstructions.Skip)]
[RetryConfig(MaxRetries = 5, RetryAfterMinutes = 10)]
public class SendCustomerEmailsJob : IScheduledJob<SendEmailsParams>
{
    private readonly AppDbContext _db;
    public SendCustomerEmailsJob(AppDbContext db) => _db = db;

    public async Task Execute(SendEmailsParams jobParams)
    {
        var query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(jobParams.FilterByDomain))
            query = query.Where(c => c.Email.EndsWith($"@{jobParams.FilterByDomain}"));

        var customers = await query.ToListAsync();

        foreach (var c in customers)
        {
            Console.WriteLine(
                $"[SendCustomerEmailsJob] Sending '{jobParams.Subject}' to {c.Name} <{c.Email}> @ {DateTimeOffset.UtcNow:O}");
        }

        if (customers.Count == 0)
            Console.WriteLine($"[SendCustomerEmailsJob] No customers matched filter for subject '{jobParams.Subject}'.");
    }
}
