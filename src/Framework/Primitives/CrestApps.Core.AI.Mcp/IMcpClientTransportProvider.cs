using CrestApps.Core.AI.Mcp.Models;
using ModelContextProtocol.Client;

namespace CrestApps.Core.AI.Mcp;

/// <summary>
/// Provides transport implementations for MCP (Model Context Protocol) client connections.
/// Each implementation handles a specific transport type (e.g., SSE, Stdio) and
/// determines whether it can service a given <see cref="McpConnection"/>.
/// </summary>
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
    Task<IClientTransport> GetAsync(McpConnection connection);
}
