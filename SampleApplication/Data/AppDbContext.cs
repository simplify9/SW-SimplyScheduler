using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SW.Scheduler.EfCore;
using SW.Scheduler.MySql;
using SW.Scheduler.PgSql;
using SW.Scheduler.SqlServer;

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

        var provider = _configuration?.GetValue<string>("Scheduler:Provider")?.ToLowerInvariant() ?? "inmemory";
        var schema   = _configuration?.GetValue<string>("Scheduler:Schema") ?? "quartz";

        switch (provider)
        {
            case "pgsql":
                modelBuilder.UseSchedulerPostgreSql(schema);
                break;
            case "mssql":
                modelBuilder.UseSchedulerSqlServer(schema);
                break;
            case "mysql":
                modelBuilder.UseSchedulerMySql();
                break;
            default:
                modelBuilder.ApplyScheduling();
                break;
        }
    }
}
