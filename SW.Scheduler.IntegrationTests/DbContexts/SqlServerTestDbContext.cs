using Microsoft.EntityFrameworkCore;
using SW.Scheduler.SqlServer;

namespace SW.Scheduler.IntegrationTests.DbContexts;

public class SqlServerTestDbContext : DbContext
{
    private readonly SchemaOptions _schemaOptions;

    public SqlServerTestDbContext(DbContextOptions<SqlServerTestDbContext> options, SchemaOptions schemaOptions)
        : base(options)
    {
        _schemaOptions = schemaOptions;
    }

    public string Schema => _schemaOptions.Schema!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseSchedulerSqlServer(_schemaOptions.Schema!);
    }
}
