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
    /// Clustering check-in interval (default: 10 seconds)
    /// </summary>
    public TimeSpan ClusteringCheckinInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Clustering misfire threshold (default: 20 seconds)
    /// </summary>
    public TimeSpan ClusteringMisfireThreshold { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Authenticate with Azure AD (Managed Identity / Entra ID) instead of a static password —
    /// for Postgres servers such as Azure Database for PostgreSQL that require token auth.
    /// <see cref="ConnectionString"/> must not contain a Password; a fresh token is fetched and
    /// used as the password for every new physical connection Quartz opens, since Quartz's ADO
    /// job store keeps connections for the lifetime of the process with no hook to refresh a
    /// static credential.
    /// </summary>
    public bool UseAzureManagedIdentity { get; set; }

    /// <summary>
    /// Client ID of the user-assigned managed identity to authenticate as. Leave null to use
    /// <c>DefaultAzureCredential</c>'s standard fallback chain (system-assigned identity,
    /// environment variables, etc). Only used when <see cref="UseAzureManagedIdentity"/> is true.
    /// </summary>
    public string? AzureManagedIdentityClientId { get; set; }

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
