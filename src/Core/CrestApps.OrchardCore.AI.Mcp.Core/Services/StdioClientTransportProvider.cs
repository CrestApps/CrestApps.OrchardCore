using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol.Transport;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Services;

public sealed class StdioClientTransportProvider : IMcpClientTransportProvider
{
    public bool CanHandle(McpConnection connection)
    {
        return connection.Source == McpConstants.TransportTypes.StdIo;
    }

    public IClientTransport Get(McpConnection connection)
    {
        var metadata = connection.As<StdioMcpConnectionMetadata>();

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = connection.DisplayText,
            Command = metadata.Command,
            Arguments = metadata.Arguments,
        });

        return transport;
    }
}
