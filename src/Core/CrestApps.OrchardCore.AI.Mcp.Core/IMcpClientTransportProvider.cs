using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol.Transport;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public interface IMcpClientTransportProvider
{
    bool CanHandle(McpConnection connection);

    IClientTransport Get(McpConnection connection);
}
