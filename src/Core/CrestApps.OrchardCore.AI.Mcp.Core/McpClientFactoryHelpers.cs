using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public static class McpClientFactoryHelpers
{
    public static async Task<IMcpClient> CreateAsync(McpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var client = await McpClientFactory.CreateAsync(new McpServerConfig()
        {
            Id = connection.Id,
            Name = connection.DisplayText,
            TransportType = TransportTypes.StdIo,
            TransportOptions = new()
            {
                ["command"] = "npx",
                ["arguments"] = "-y @modelcontextprotocol/server-everything",
            }
        });

        return client;
    }
}
