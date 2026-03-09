namespace SW.Scheduler.EfCore.EntityTypeConfigurations;

/// <summary>
/// Holds the provider-specific SQL column type names used when building
/// Quartz entity type configurations.
///
/// Each provider package (PgSql, SqlServer, MySql …) creates an instance
/// of this class with its own type names and passes it to the shared
/// <c>Quartz*EntityTypeConfiguration</c> classes in this assembly.
/// </summary>
public class QuartzColumnTypes
{
    /// <summary>Variable-length text/string type. e.g. "text", "nvarchar(max)", "varchar(255)".</summary>
    public string Text { get; init; } = "text";

    /// <summary>Boolean type. e.g. "bool", "bit", "tinyint(1)".</summary>
    public string Bool { get; init; } = "bool";

    /// <summary>64-bit integer type. e.g. "bigint".</summary>
    public string BigInt { get; init; } = "bigint";

    /// <summary>32-bit integer type. e.g. "integer", "int".</summary>
    public string Int { get; init; } = "integer";

    /// <summary>Binary/blob type used for job/calendar data. e.g. "bytea", "varbinary(max)", "longblob".</summary>
    public string Blob { get; init; } = "bytea";

    /// <summary>Unbounded text type for the Context/JSON column. e.g. "text", "nvarchar(max)", "longtext".</summary>
    public string UnboundedText { get; init; } = "text";

    // ── Pre-built provider profiles ───────────────────────────────────────────

    /// <summary>Column types for PostgreSQL.</summary>
    public static readonly QuartzColumnTypes PostgreSql = new()
    {
        Text          = "text",
        Bool          = "bool",
        BigInt        = "bigint",
        Int           = "integer",
        Blob          = "bytea",
        UnboundedText = "text"
    };

    /// <summary>Column types for SQL Server.</summary>
    public static readonly QuartzColumnTypes SqlServer = new()
    {
        Text          = "nvarchar(450)",
        Bool          = "bit",
        BigInt        = "bigint",
        Int           = "int",
        Blob          = "varbinary(max)",
        UnboundedText = "nvarchar(max)"
    };

    /// <summary>Column types for MySQL / MariaDB.</summary>
    public static readonly QuartzColumnTypes MySql = new()
    {
        Text          = "varchar(200)",
        Bool          = "tinyint(1)",
        BigInt        = "bigint",
        Int           = "int",
        Blob          = "longblob",
        UnboundedText = "longtext"
    };
}

