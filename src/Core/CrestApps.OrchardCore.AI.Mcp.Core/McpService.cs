using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public class McpService
{
    private readonly Dictionary<string, IMcpClient> _clients = [];
    private readonly Dictionary<string, IEnumerable<AITool>> _tools = [];
    private readonly IEnumerable<IMcpClientTransportProvider> _providers;

    public McpService(IEnumerable<IMcpClientTransportProvider> providers)
    {
        _providers = providers;
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
                throw new InvalidOperationException($"Unable to find an implementation of '{nameof(IMcpClientTransportProvider)}' that supports the connection. Not supported transport type.");
            }

            client = await McpClientFactory.CreateAsync(transport);

            _clients[connection.Id] = client;
        }

        return client;
    }

    public async Task<IEnumerable<AITool>> GetToolsAsync(McpConnection connection)
    {
        if (!_tools.TryGetValue(connection.Id, out var tools))
        {
            var client = await GetOrCreateClientAsync(connection);

            tools = await client.ListToolsAsync();

            _tools[connection.Id] = tools;
        }

        return tools;
    }
}
