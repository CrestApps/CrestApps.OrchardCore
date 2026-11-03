using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.DataConnectors;

internal sealed class DataConnectorManager : IDataConnectorManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<DataConnectorDescriptor> _connectors;
    private readonly Dictionary<string, DataConnectorDescriptor> _byName;

    public DataConnectorManager(
        IOptions<DataConnectorOptions> options,
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _connectors = options.Value.Connectors;
        _byName = new Dictionary<string, DataConnectorDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var connector in _connectors)
        {
            if (!_byName.TryAdd(connector.Name, connector))
            {
                throw new InvalidOperationException($"More than one data connector is registered with the name '{connector.Name}'.");
            }
        }
    }

    public IReadOnlyList<DataConnectorDescriptor> GetConnectors() => _connectors;

    public DataConnectorDescriptor GetDescriptor(string name)
        => string.IsNullOrEmpty(name) ? null : _byName.GetValueOrDefault(name);

    public IDataConnector GetConnector(string name)
        => GetDescriptor(name) is { } descriptor
            ? (IDataConnector)_serviceProvider.GetRequiredService(descriptor.Type)
            : null;
}
