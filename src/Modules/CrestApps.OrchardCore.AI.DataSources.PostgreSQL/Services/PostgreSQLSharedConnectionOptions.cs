namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;

/// <summary>
/// What a tenant other than the default one may do with PostgreSQL data sources, bound from the
/// <c>CrestApps:AI:DataSources:PostgreSQL</c> section of the tenant's configuration, which only the host's operator
/// controls. The default tenant needs none.
/// </summary>
internal sealed class PostgreSQLSharedConnectionOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:AI:DataSources:PostgreSQL";

    /// <summary>
    /// Gets or sets the tables (<c>table</c> or <c>schema.table</c>, as the data source names them) a tenant other than
    /// the default one may read through the globally configured connection string. Empty by default, so such a tenant
    /// can't use the global connection string.
    /// </summary>
    public List<string> SharedConnectionTables { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a tenant other than the default one may enter a connection string that
    /// signs in as the host (no password, so the host's password file, Kerberos ticket or Windows account, or a client
    /// certificate, password file or root certificate read from the host's disk). Off by default.
    /// </summary>
    public bool AllowHostIdentity { get; set; }
}
