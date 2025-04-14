using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol.Transport;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public interface IMcpClientTransportProvider
{
    /// <summary>
    /// Determines whether this provider can handle the specified <see cref="McpConnection"/>.
    /// </summary>
    /// <param name="connection">The MCP connection to evaluate.</param>
    /// <returns>
    /// <c>true</c> if this provider supports the given connection; otherwise, <c>false</c>.
    /// </returns>
    bool CanHandle(McpConnection connection);

    /// <summary>
    /// Gets an <see cref="IClientTransport"/> instance for the specified <see cref="McpConnection"/>.
    /// </summary>
    /// <param name="connection">The MCP connection for which to obtain a transport.</param>
    /// <returns>An <see cref="IClientTransport"/> that can be used with the given connection.</returns>
    IClientTransport Get(McpConnection connection);
}
