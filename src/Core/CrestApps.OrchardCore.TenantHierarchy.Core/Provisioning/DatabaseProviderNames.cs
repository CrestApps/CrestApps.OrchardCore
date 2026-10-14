namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Contains the Orchard Core database provider names the provisioners use.
/// </summary>
public static class DatabaseProviderNames
{
    /// <summary>
    /// The SQLite provider.
    /// </summary>
    public const string Sqlite = "Sqlite";

    /// <summary>
    /// The SQL Server provider.
    /// </summary>
    public const string SqlServer = "SqlConnection";

    /// <summary>
    /// The PostgreSQL provider.
    /// </summary>
    public const string PostgreSql = "Postgres";

    /// <summary>
    /// The MySQL provider.
    /// </summary>
    public const string MySql = "MySql";
}
