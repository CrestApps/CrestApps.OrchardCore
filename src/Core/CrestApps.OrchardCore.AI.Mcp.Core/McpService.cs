using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

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
            IClientTransport transport = connection.TransportType switch
            {
                McpConstants.TransportTypes.StdIo => new StdioClientTransport(new StdioClientTransportOptions()
                {
                    Name = connection.DisplayText,
                    Command = "npx",
                    EnvironmentVariables = connection.TransportOptions,
                }),
                McpConstants.TransportTypes.Sse => new SseClientTransport(new SseClientTransportOptions()
                {
                    Name = connection.DisplayText,
                    Endpoint = new Uri(connection.Location),
                }),
                _ => throw new InvalidOperationException("Not supported transport type"),
            };

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
