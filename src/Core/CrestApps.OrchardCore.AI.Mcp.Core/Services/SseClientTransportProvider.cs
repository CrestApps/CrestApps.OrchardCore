using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol.Transport;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Services;

public sealed class SseClientTransportProvider : IMcpClientTransportProvider
{
    public bool CanHandle(McpConnection connection)
        => connection.Source == McpConstants.TransportTypes.Sse;
    
    public IClientTransport Get(McpConnection connection)
    {
        var metadata = connection.As<SseMcpConnectionMetadata>();

        var transport = new SseClientTransport(new SseClientTransportOptions()
        {
            Endpoint = metadata.Endpoint,
            AdditionalHeaders = metadata.AdditionalHeaders,
        });

        return transport;
    }
}
