namespace SW.Scheduler.PgSql;

/// <summary>
/// Configuration options for Quartz PostgreSQL scheduler
/// </summary>
public class QuartzPgSqlOptions
{
    /// <summary>
    /// PostgreSQL connection string (required)
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database schema for Quartz tables (required for PostgreSQL)
    /// </summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// Table prefix for Quartz tables (default: "qrtz_")
    /// </summary>
    public string TablePrefix { get; set; } = "qrtz_";

    /// <summary>
    /// Enable clustering for multiple scheduler instances (default: false)
    /// </summary>
    public bool EnableClustering { get; set; } = false;

    /// <summary>
    /// Clustering check-in interval (default: 10 seconds)
    /// </summary>
    public TimeSpan ClusteringCheckinInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Clustering misfire threshold (default: 20 seconds)
    /// </summary>
    public TimeSpan ClusteringMisfireThreshold { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Validate the options
    /// </summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("ConnectionString is required", nameof(ConnectionString));

        if (string.IsNullOrWhiteSpace(Schema))
            throw new ArgumentException("Schema is required for PostgreSQL", nameof(Schema));

        if (string.IsNullOrWhiteSpace(TablePrefix))
            throw new ArgumentException("TablePrefix cannot be empty", nameof(TablePrefix));
    }
}
