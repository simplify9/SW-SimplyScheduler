using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SW.Scheduler.IntegrationTests.DbContexts;

/// <summary>
/// Includes the per-test schema name in the EF Core model cache key so that each
/// test's DbContext gets a freshly compiled model rather than reusing the cached
/// model from a previous test that used a different schema.
/// </summary>
internal sealed class SchemaAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context switch
        {
            PgSqlTestDbContext pg => (typeof(PgSqlTestDbContext), pg.Schema, designTime),
            SqlServerTestDbContext ss => (typeof(SqlServerTestDbContext), ss.Schema, designTime),
            _ => (context.GetType(), designTime)
        };
}
