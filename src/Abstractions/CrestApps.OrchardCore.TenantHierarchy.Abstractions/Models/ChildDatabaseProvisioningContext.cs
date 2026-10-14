namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes the database a provisioner must create or remove for a child tenant.
/// </summary>
public sealed class ChildDatabaseProvisioningContext
{
    /// <summary>
    /// Gets or sets the opaque tenant name of the child tenant.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the strategy of the parent policy.
    /// </summary>
    public ChildDatabaseStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets the name of the pool, or <see langword="null"/> for <see cref="ChildDatabaseStrategy.SqlitePerChild"/>.
    /// </summary>
    public string PoolName { get; set; }

    /// <summary>
    /// Gets or sets the pool, or <see langword="null"/> for <see cref="ChildDatabaseStrategy.SqlitePerChild"/>.
    /// </summary>
    public DatabasePoolOptions Pool { get; set; }

    /// <summary>
    /// Gets or sets the database, schema or table prefix that was created, when a resource is removed.
    /// </summary>
    public string ProvisionedResource { get; set; }
}
