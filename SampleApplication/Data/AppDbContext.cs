using Microsoft.EntityFrameworkCore;
using SW.Scheduler.EfCore;

namespace SampleApplication.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Your application entities
        modelBuilder.Entity<Customer>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.Property(c => c.Email).IsRequired().HasMaxLength(320);
            b.HasIndex(c => c.Email).IsUnique();
        });

        // Include Quartz tables + JobExecution monitoring table in this DbContext's migrations.
        // Pass the schema name when using PostgreSQL (optional for InMemory/SQLite).
        // For PostgreSQL with a specific schema, use: modelBuilder.UseQuartzPostgreSql("quartz")
        // For a database-agnostic setup, use: modelBuilder.ApplyScheduling()
        modelBuilder.ApplyScheduling();
    }
}
