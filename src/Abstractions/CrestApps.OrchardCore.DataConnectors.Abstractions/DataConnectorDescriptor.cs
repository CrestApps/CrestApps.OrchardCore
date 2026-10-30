using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Describes a registered data connector.
/// </summary>
public sealed class DataConnectorDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorDescriptor"/> class.
    /// </summary>
    /// <param name="name">The technical name of the connector.</param>
    /// <param name="displayName">The name shown to users.</param>
    /// <param name="description">The description shown to users.</param>
    /// <param name="capabilities">What the connector can do.</param>
    /// <param name="type">The connector's implementation type.</param>
    public DataConnectorDescriptor(
        string name,
        LocalizationSource displayName,
        LocalizationSource description,
        DataConnectorCapabilities capabilities,
        Type type)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(type);

        Name = name;
        DisplayName = displayName;
        Description = description;
        Capabilities = capabilities;
        Type = type;
    }

    /// <summary>
    /// Gets the technical name of the connector.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name shown to users.
    /// </summary>
    public LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the description shown to users.
    /// </summary>
    public LocalizationSource Description { get; }

    /// <summary>
    /// Gets what the connector can do.
    /// </summary>
    public DataConnectorCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the connector's implementation type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Returns whether the connector has every one of the capabilities.
    /// </summary>
    /// <param name="capabilities">The capabilities to check.</param>
    /// <returns><see langword="true"/> when the connector has them all.</returns>
    public bool Supports(DataConnectorCapabilities capabilities)
        => (Capabilities & capabilities) == capabilities;

    /// <summary>
    /// Creates the descriptor of a connector type from its static members.
    /// </summary>
    /// <typeparam name="TConnector">The connector type.</typeparam>
    /// <returns>The descriptor.</returns>
    public static DataConnectorDescriptor Create<TConnector>()
        where TConnector : class, IDataConnector
        => new(TConnector.Name, TConnector.DisplayName, TConnector.Description, TConnector.Capabilities, typeof(TConnector));
}

/// <summary>
/// The data connectors registered on a tenant.
/// </summary>
public sealed class DataConnectorOptions
{
    private readonly List<DataConnectorDescriptor> _connectors = [];

    /// <summary>
    /// Gets the registered connectors.
    /// </summary>
    public IReadOnlyList<DataConnectorDescriptor> Connectors => _connectors;

    /// <summary>
    /// Registers a connector, once per type.
    /// </summary>
    /// <param name="descriptor">The connector's descriptor.</param>
    /// <returns>The options.</returns>
    public DataConnectorOptions Add(DataConnectorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!_connectors.Any(connector => connector.Type == descriptor.Type))
        {
            _connectors.Add(descriptor);
        }

        return this;
    }
}
