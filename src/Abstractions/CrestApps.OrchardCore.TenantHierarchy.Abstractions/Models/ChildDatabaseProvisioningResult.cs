namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Holds the database settings a provisioner produced for a child tenant.
/// </summary>
public sealed class ChildDatabaseProvisioningResult
{
    /// <summary>
    /// Gets or sets the error, or <see langword="null"/> when the database was provisioned.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets a value indicating whether the database was provisioned.
    /// </summary>
    public bool Succeeded => Error == null;

    /// <summary>
    /// Gets or sets the Orchard Core database provider name.
    /// </summary>
    public string DatabaseProvider { get; set; }

    /// <summary>
    /// Gets or sets the connection string the child tenant uses.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the table prefix the child tenant uses.
    /// </summary>
    public string TablePrefix { get; set; }

    /// <summary>
    /// Gets or sets the schema the child tenant uses.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets the SQLite database file name.
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the name of the database, schema or table prefix that was created, so it can be removed later.
    /// </summary>
    public string ProvisionedResource { get; set; }

    /// <summary>
    /// Creates a result for a provisioning that failed.
    /// </summary>
    /// <param name="error">The error message.</param>
    public static ChildDatabaseProvisioningResult Failed(string error)
    {
        return new ChildDatabaseProvisioningResult
        {
            Error = error,
        };
    }
}
