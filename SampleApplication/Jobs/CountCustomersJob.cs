using Microsoft.EntityFrameworkCore;
using SampleApplication.Data;
using SW.PrimitiveTypes; // contains IScheduledJob

namespace SampleApplication.Jobs;

[Schedule("0 * * * * ?", TriggerKey = "count-customers-every-minute", Description = "Count customers every minute")]
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

