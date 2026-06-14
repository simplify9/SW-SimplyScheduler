namespace SW.Scheduler.IntegrationTests.DbContexts;

/// <summary>
/// Carries the per-test schema (or database) name into the test DbContext
/// via the DI container so each test gets a fully isolated Quartz table set.
/// </summary>
public sealed class SchemaOptions
{
    public string? Schema { get; }

    public SchemaOptions(string? schema) => Schema = schema;
}
