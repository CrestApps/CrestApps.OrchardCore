using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Tools;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Mcp.Strategies;

/// <summary>
/// A prompt processing strategy that injects MCP capability metadata into the
/// completion context when the intent is <see cref="DocumentIntents.LookingForExternalCapabilities"/>.
/// </summary>
/// <remarks>
/// <para>Uses pre-intent resolution results to identify which MCP connections are relevant,
/// then fetches full structured metadata for those connections and injects it as context
/// so the final AI response can invoke specific MCP capabilities.</para>
/// </remarks>
public sealed class McpCapabilitiesProcessingStrategy : NamedPromptProcessingStrategy
{
    private readonly ISourceCatalog<McpConnection> _store;
    private readonly IMcpServerMetadataCacheProvider _metadataProvider;
    private readonly IMcpMetadataPromptGenerator _promptGenerator;
    private readonly ILogger _logger;

    public McpCapabilitiesProcessingStrategy(
        ISourceCatalog<McpConnection> store,
        IMcpServerMetadataCacheProvider metadataProvider,
        IMcpMetadataPromptGenerator promptGenerator,
        ILogger<McpCapabilitiesProcessingStrategy> logger)
        : base(DocumentIntents.LookingForExternalCapabilities)
    {
        _store = store;
        _metadataProvider = metadataProvider;
        _promptGenerator = promptGenerator;
        _logger = logger;
    }

    protected override async Task ProceedAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
    {
        var mcpConnectionIds = context.CompletionContext?.McpConnectionIds;

        if (mcpConnectionIds is null || mcpConnectionIds.Length == 0)
        {
            return;
        }

        var connections = (await _store.GetAllAsync())
            .ToDictionary(x => x.ItemId);

        if (connections.Count == 0)
        {
            return;
        }

        // Resolve configured MCP connections for this profile.
        var configuredConnections = new List<McpConnection>();

        foreach (var mcpConnectionId in mcpConnectionIds)
        {
            if (connections.TryGetValue(mcpConnectionId, out var connection))
            {
                configuredConnections.Add(connection);
            }
        }

        if (configuredConnections.Count == 0)
        {
            return;
        }

        // Fetch capabilities from cache for all configured connections.
        var capabilitiesByConnection = new List<McpServerCapabilities>();

        foreach (var connection in configuredConnections)
        {
            try
            {
                var capabilities = await _metadataProvider.GetCapabilitiesAsync(connection);

                if (capabilities is not null)
                {
                    capabilitiesByConnection.Add(capabilities);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to get capabilities from MCP connection Id '{ConnectionId}' and Name: '{ConnectionName}'.", connection.ItemId, connection.DisplayText);
            }
        }

        if (capabilitiesByConnection.Count == 0)
        {
            return;
        }

        List<McpServerCapabilities> relevantCapabilities;

        // Use pre-intent resolution results to narrow down to relevant connections.
        if (context.PreIntentResolution is not null &&
            context.PreIntentResolution.HasRelevantCapabilities)
        {
            var preResolvedIds = context.PreIntentResolution.RelevantSourceIds;

            relevantCapabilities = capabilitiesByConnection
                .Where(c => preResolvedIds.Contains(c.ConnectionId))
                .ToList();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Using {Count} pre-resolved connection(s) from capability resolution.",
                    relevantCapabilities.Count);
            }

            // If pre-resolution filtered to zero, fall back to all configured capabilities.
            if (relevantCapabilities.Count == 0)
            {
                relevantCapabilities = capabilitiesByConnection;
            }
        }
        else
        {
            // No pre-intent resolution available; use all configured capabilities.
            relevantCapabilities = capabilitiesByConnection;
        }

        if (relevantCapabilities.Count == 0)
        {
            return;
        }

        var metadataPrompt = _promptGenerator.Generate(relevantCapabilities);

        if (string.IsNullOrEmpty(metadataPrompt))
        {
            return;
        }

        context.Result.AddContext(metadataPrompt);
        context.Result.ToolNames.Add(McpInvokeFunction.FunctionName);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Injected MCP capabilities from {Count} connection(s) into prompt context.", relevantCapabilities.Count);
        }
    }
}
