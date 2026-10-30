using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.DataConnectors.Models;

/// <summary>
/// How a tenant reaches one external system: which connector, its settings and its secrets.
/// </summary>
/// <remarks>
/// The connector's settings are stored in <see cref="CrestApps.Core.ExtensibleEntity.Properties"/>. One connection
/// can serve many data sets.
/// </remarks>
public sealed class DataConnection : CatalogItem
{
    /// <summary>
    /// Gets or sets the identifier of the stored document, so a record saved again after a commit updates its row.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the connection, unique on the tenant.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown to users.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description of the connection.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the name of the connector that reads the external system.
    /// </summary>
    public string Connector { get; set; }

    /// <summary>
    /// Gets or sets the connection's secrets by name, each protected with the tenant's data protection keys.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets when the connection was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the connection was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user name of whoever created the connection.
    /// </summary>
    public string Author { get; set; }
}
