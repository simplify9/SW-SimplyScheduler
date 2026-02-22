using Microsoft.EntityFrameworkCore;
using SW.Scheduler.PgSql;

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

        // Add Quartz.NET scheduler tables to this DbContext
        // This will include Quartz tables in YOUR migrations
        modelBuilder.UseQuartzPostgreSql(schema: "quartz");
    }
}

