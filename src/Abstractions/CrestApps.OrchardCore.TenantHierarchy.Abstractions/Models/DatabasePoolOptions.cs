namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes a database server that child tenants are placed on. Pools are configured on the host only, under
/// <c>TenantHierarchy:DatabasePools:{name}</c>, so their credentials are never shown to a tenant.
/// </summary>
public sealed class DatabasePoolOptions
{
    /// <summary>
    /// Gets or sets the Orchard Core database provider name, for example <c>SqlConnection</c> or <c>Postgres</c>.
    /// </summary>
    public string DatabaseProvider { get; set; }

    /// <summary>
    /// Gets or sets the connection string of the pool. For <see cref="ChildDatabaseStrategy.DatabasePerChild"/> and
    /// <see cref="ChildDatabaseStrategy.SchemaPerChild"/> it must belong to a login that can create databases,
    /// schemas and logins.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a removed child database is renamed and kept instead of dropped.
    /// </summary>
    public bool RetainRemovedDatabases { get; set; } = true;
}
