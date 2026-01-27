using Microsoft.EntityFrameworkCore;
using SampleApplication.Data;
using SW.PrimitiveTypes; // contains IScheduledJob

namespace SampleApplication.Jobs;

public class CountCustomersJob : IScheduledJob
{
    private readonly AppDbContext _db;
    public CountCustomersJob(AppDbContext db) => _db = db;

    // Run every minute at second 0
    public string DefaultSchedule() => "0 * * * * ?";

    public async Task Execute()
    {
        var total = await _db.Customers.CountAsync();
        Console.WriteLine($"[CountCustomersJob] Total customers: {total} @ {DateTimeOffset.UtcNow:O}");
    }
}

