using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Tools;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Provides MCP server tool metadata to the unified tool registry.
/// Reads cached capabilities from configured MCP connections and
/// produces <see cref="ToolRegistryEntry"/> instances for each tool.
/// Each entry carries a <see cref="ToolRegistryEntry.ToolFactory"/> that creates
/// a <see cref="McpToolProxyFunction"/> to transparently route calls to the MCP server.
/// </summary>
internal sealed class McpToolRegistryProvider : IToolRegistryProvider
{
    private static readonly JsonElement _emptySchema = JsonSerializer.Deserialize<JsonElement>(
        """{"type": "object", "properties": {}, "additionalProperties": false}""");

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

                var connectionId = connection.ItemId;

                foreach (var tool in capabilities.Tools)
                {
                    if (string.IsNullOrWhiteSpace(tool.Name))
                    {
                        continue;
                    }

                    var toolName = tool.Name;
                    var toolDescription = tool.Description ?? toolName;
                    var toolSchema = tool.InputSchema ?? _emptySchema;

                    entries.Add(new ToolRegistryEntry
                    {
                        Id = $"mcp:{connectionId}:{toolName}",
                        Name = toolName,
                        Description = toolDescription,
                        Source = ToolRegistryEntrySource.McpServer,
                        SourceId = connectionId,
                        ToolFactory = (_) => ValueTask.FromResult<AITool>(
                            new McpToolProxyFunction(toolName, toolDescription, toolSchema, connectionId)),
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
