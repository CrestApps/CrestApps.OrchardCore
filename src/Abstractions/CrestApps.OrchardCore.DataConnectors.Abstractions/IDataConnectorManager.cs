namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Finds the registered data connectors.
/// </summary>
public interface IDataConnectorManager
{
    /// <summary>
    /// Returns every registered connector.
    /// </summary>
    /// <returns>The connector descriptors.</returns>
    IReadOnlyList<DataConnectorDescriptor> GetConnectors();

    /// <summary>
    /// Returns the descriptor of a connector by name.
    /// </summary>
    /// <param name="name">The connector's name, ignoring case.</param>
    /// <returns>The descriptor, or <see langword="null"/> when no connector has the name.</returns>
    DataConnectorDescriptor GetDescriptor(string name);

    /// <summary>
    /// Returns a connector by name.
    /// </summary>
    /// <param name="name">The connector's name, ignoring case.</param>
    /// <returns>The connector, or <see langword="null"/> when no connector has the name.</returns>
    IDataConnector GetConnector(string name);
}
