using System.Data.Common;
using Azure.Core;
using Azure.Identity;
using Npgsql;
using Quartz.Impl.AdoJobStore.Common;

namespace SW.Scheduler.PgSql;

/// <summary>
/// Postgres <see cref="IDbProvider"/> that authenticates every new physical connection with a
/// fresh Azure AD access token instead of a static password. Required for Postgres servers
/// (e.g. Azure Database for PostgreSQL) configured for Entra ID authentication: Quartz's ADO job
/// store otherwise opens <see cref="NpgsqlConnection"/>s straight from a fixed connection string
/// with no hook to refresh credentials, so a token embedded once at startup would stop working
/// as soon as it expires (~1 hour) and the scheduler needed a brand-new physical connection.
/// <see cref="DefaultAzureCredential"/> caches/refreshes tokens internally, so fetching one on
/// every call here is cheap.
/// </summary>
public sealed class AzureManagedIdentityNpgsqlDbProvider : DbProvider
{
    private static readonly string[] Scopes = { "https://ossrdbms-aad.database.windows.net/.default" };

    private TokenCredential? _credential;

    /// <summary>
    /// Client ID of the user-assigned managed identity to authenticate as, or null to use
    /// <see cref="DefaultAzureCredential"/>'s standard fallback chain.
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }

    public AzureManagedIdentityNpgsqlDbProvider() : base("Npgsql", string.Empty)
    {
    }

    public override DbConnection CreateConnection()
    {
        _credential ??= string.IsNullOrEmpty(ManagedIdentityClientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = ManagedIdentityClientId });

        var token = _credential.GetToken(new TokenRequestContext(Scopes), default).Token;
        var connectionStringWithToken = new NpgsqlConnectionStringBuilder(ConnectionString) { Password = token }.ConnectionString;
        return new NpgsqlConnection(connectionStringWithToken);
    }
}
