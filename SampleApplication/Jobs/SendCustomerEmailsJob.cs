using Microsoft.EntityFrameworkCore;
using SampleApplication.Data;
using SW.PrimitiveTypes;

namespace SampleApplication.Jobs;

public class SendCustomerEmailsJob : IScheduledJob
{
    private readonly AppDbContext _db;
    public SendCustomerEmailsJob(AppDbContext db) => _db = db;

    // Run every minute at second 30
    public string DefaultSchedule() => "30 * * * * ?";

    public async Task Execute()
    {
        var customers = await _db.Customers.AsNoTracking().ToListAsync();
        foreach (var c in customers)
        {
            Console.WriteLine($"[SendCustomerEmailsJob] Email sent successfully to {c.Name} <{c.Email}> @ {DateTimeOffset.UtcNow:O}");
        }
        if (customers.Count == 0)
            Console.WriteLine("[SendCustomerEmailsJob] No customers to email.");
    }
}

