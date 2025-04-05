using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public class McpService
{
    private readonly Dictionary<string, IMcpClient> _clients = [];

    private readonly Dictionary<string, IEnumerable<AITool>> _tools = [];

    public async Task<IMcpClient> GetOrCreateClientAsync(McpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!_clients.TryGetValue(connection.Id, out var client))
        {
            client = await McpClientFactory.CreateAsync(new McpServerConfig()
            {
                Id = connection.Id,
                Name = connection.DisplayText,
                TransportType = connection.TransportType,
                TransportOptions = connection.TransportOptions,
            });

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
