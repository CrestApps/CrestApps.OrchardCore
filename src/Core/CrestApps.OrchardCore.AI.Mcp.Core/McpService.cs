using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public sealed class McpService
{
    private readonly Dictionary<string, IMcpClient> _clients = [];
    private readonly IEnumerable<IMcpClientTransportProvider> _providers;
    private readonly ILogger _logger;

    public McpService(
        IEnumerable<IMcpClientTransportProvider> providers,
        ILogger<McpService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<IMcpClient> GetOrCreateClientAsync(McpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!_clients.TryGetValue(connection.Id, out var client))
        {
            IClientTransport transport = null;

            foreach (var provider in _providers)
            {
                if (!provider.CanHandle(connection))
                {
                    continue;
                }

                transport = provider.Get(connection);
            }

            if (transport is null)
            {
                _logger.LogWarning("Unable to find an implementation of '{TypeName}' that supports the connection. Not supported transport type.", nameof(IMcpClientTransportProvider));

                return null;
            }

            client = await McpClientFactory.CreateAsync(transport);

            _clients[connection.Id] = client;
        }

        return client;
    }
}
