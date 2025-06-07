using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public sealed class McpService
{
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

        return await McpClientFactory.CreateAsync(transport);
    }
}
