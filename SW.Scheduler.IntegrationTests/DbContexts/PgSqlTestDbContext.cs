using Microsoft.EntityFrameworkCore;
using SW.Scheduler.PgSql;

namespace SW.Scheduler.IntegrationTests.DbContexts;

public class PgSqlTestDbContext : DbContext
{
    private readonly SchemaOptions _schemaOptions;

    public PgSqlTestDbContext(DbContextOptions<PgSqlTestDbContext> options, SchemaOptions schemaOptions)
        : base(options)
    {
        _schemaOptions = schemaOptions;
    }

    public string Schema => _schemaOptions.Schema!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Creates Quartz tables + job_executions in the per-test schema.
        modelBuilder.UseSchedulerPostgreSql(_schemaOptions.Schema!);
    }
}
