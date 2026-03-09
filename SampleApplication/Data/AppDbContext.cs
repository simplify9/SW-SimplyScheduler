using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SW.Scheduler.EfCore;
using SW.Scheduler.PgSql;

namespace SampleApplication.Data;

public class AppDbContext : DbContext
{
    private readonly IConfiguration? _configuration;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.Property(c => c.Email).IsRequired().HasMaxLength(320);
            b.HasIndex(c => c.Email).IsUnique();
        });

        // Apply scheduler tables using the provider-specific extension when running
        // against PostgreSQL, or the provider-agnostic fallback for InMemory/testing.
        var useDatabase  = _configuration?.GetValue<bool>("Scheduler:UseDatabase") ?? false;
        var schema       = _configuration?.GetValue<string>("Scheduler:Schema") ?? "quartz";

        if (useDatabase && !Database.IsInMemory())
            modelBuilder.UseSchedulerPostgreSql(schema);
        else
            modelBuilder.ApplyScheduling();
    }
}
