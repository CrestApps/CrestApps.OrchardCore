using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Provides MCP server tool metadata to the unified tool registry.
/// Reads cached capabilities from configured MCP connections and
/// produces <see cref="ToolRegistryEntry"/> instances for each tool.
/// </summary>
internal sealed class McpToolRegistryProvider : IToolRegistryProvider
{
    private readonly IMcpServerMetadataCacheProvider _metadataProvider;
    private readonly ISourceCatalog<McpConnection> _store;
    private readonly ILogger _logger;

    public McpToolRegistryProvider(
        IMcpServerMetadataCacheProvider metadataProvider,
        ISourceCatalog<McpConnection> store,
        ILogger<McpToolRegistryProvider> logger)
    {
        _metadataProvider = metadataProvider;
        _store = store;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var mcpConnectionIds = context?.McpConnectionIds;

        if (mcpConnectionIds is null || mcpConnectionIds.Length == 0)
        {
            return [];
        }

        var connections = await _store.GetAsync(mcpConnectionIds);

        if (connections.Count == 0)
        {
            return [];
        }

        var entries = new List<ToolRegistryEntry>();

        foreach (var connection in connections)
        {
            try
            {
                var capabilities = await _metadataProvider.GetCapabilitiesAsync(connection);

                if (capabilities?.Tools is null || capabilities.Tools.Count == 0)
                {
                    continue;
                }

                foreach (var tool in capabilities.Tools)
                {
                    if (string.IsNullOrWhiteSpace(tool.Name))
                    {
                        continue;
                    }

                    entries.Add(new ToolRegistryEntry
                    {
                        Name = tool.Name,
                        Description = tool.Description ?? tool.Name,
                        Source = ToolRegistryEntrySource.McpServer,
                        SourceId = connection.ItemId,
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to load MCP tool metadata from connection '{ConnectionId}'. Skipping.",
                    connection.ItemId);
            }
        }

        return entries;
    }
}
