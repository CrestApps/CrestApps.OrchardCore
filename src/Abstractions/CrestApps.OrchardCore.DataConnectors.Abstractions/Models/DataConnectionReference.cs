namespace CrestApps.OrchardCore.DataConnectors.Models;

/// <summary>
/// Stored in the properties of another module's record, such as an AI data source, to say that the record reaches
/// its external system through a <see cref="DataConnection"/> rather than through an endpoint and credentials of its
/// own. A record without it keeps using its own settings, so adding the option never changes an existing record.
/// </summary>
public sealed class DataConnectionReference
{
    /// <summary>
    /// Gets or sets the identifier of the data connection.
    /// </summary>
    public string ConnectionId { get; set; }
}
