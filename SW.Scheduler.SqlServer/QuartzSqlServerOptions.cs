namespace SW.Scheduler.SqlServer;

/// <summary>
/// Configuration options for the SQL Server-backed Quartz scheduler.
/// </summary>
public class QuartzSqlServerOptions
{
    /// <summary>SQL Server connection string (required).</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database schema for Quartz tables (optional, defaults to "dbo").
    /// </summary>
    public string Schema { get; set; } = "dbo";

    /// <summary>Table prefix for Quartz tables (default: "QRTZ_").</summary>
    public string TablePrefix { get; set; } = "QRTZ_";

    /// <summary>Enable clustering for multiple scheduler instances (default: false).</summary>
    public bool EnableClustering { get; set; } = false;

    /// <summary>Clustering check-in interval (default: 10 seconds).</summary>
    public TimeSpan ClusteringCheckinInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Clustering misfire threshold (default: 20 seconds).</summary>
    public TimeSpan ClusteringMisfireThreshold { get; set; } = TimeSpan.FromSeconds(20);

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("ConnectionString is required", nameof(ConnectionString));
        if (string.IsNullOrWhiteSpace(TablePrefix))
            throw new ArgumentException("TablePrefix cannot be empty", nameof(TablePrefix));
    }
}

