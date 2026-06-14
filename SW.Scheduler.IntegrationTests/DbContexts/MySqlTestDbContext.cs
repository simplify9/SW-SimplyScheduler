using Microsoft.EntityFrameworkCore;
using SW.Scheduler.MySql;

namespace SW.Scheduler.IntegrationTests.DbContexts;

public class MySqlTestDbContext : DbContext
{
    public MySqlTestDbContext(DbContextOptions<MySqlTestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MySQL has no schemas; isolation is done via per-test databases.
        modelBuilder.UseSchedulerMySql();
    }
}
