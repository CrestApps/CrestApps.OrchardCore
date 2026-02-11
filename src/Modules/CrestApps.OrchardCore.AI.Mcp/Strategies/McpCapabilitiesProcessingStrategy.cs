using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Tools;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Mcp.Strategies;

/// <summary>
/// A second-phase prompt processing strategy for MCP capability routing.
/// </summary>
/// <remarks>
/// <para>This strategy runs only when the second phase is triggered, either because the AI
/// classifier detected <see cref="DocumentIntents.LookingForExternalCapabilities"/>, or because
/// a first-phase strategy set <see cref="IntentProcessingResult.RequiresSecondPhase"/>.</para>
///
/// <para>It makes a lightweight AI call (no chat history) with MCP capability metadata
/// (names and descriptions only) to identify which connections can handle the user's request.
/// Only the matching connections' full metadata is injected into the completion context.</para>
/// </remarks>
public sealed class McpCapabilitiesProcessingStrategy : IPromptProcessingStrategy
{
    private readonly ISourceCatalog<McpConnection> _store;
    private readonly IMcpServerMetadataCacheProvider _metadataProvider;
    private readonly IMcpMetadataPromptGenerator _promptGenerator;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public McpCapabilitiesProcessingStrategy(
        ISourceCatalog<McpConnection> store,
        IMcpServerMetadataCacheProvider metadataProvider,
        IMcpMetadataPromptGenerator promptGenerator,
        IAIClientFactory aiClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<McpCapabilitiesProcessingStrategy> logger)
    {
        _store = store;
        _metadataProvider = metadataProvider;
        _promptGenerator = promptGenerator;
        _aiClientFactory = aiClientFactory;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
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

        // Make a lightweight AI call to identify which capabilities match the user's prompt.
        var matchedConnectionIds = await ResolveCapabilitiesAsync(
            context, capabilitiesByConnection, cancellationToken);

        List<McpServerCapabilities> relevantCapabilities;

        if (matchedConnectionIds is not null && matchedConnectionIds.Count > 0)
        {
            // Only inject the connections the AI identified as relevant.
            relevantCapabilities = capabilitiesByConnection
                .Where(c => matchedConnectionIds.Contains(c.ConnectionId))
                .ToList();
        }
        else
        {
            // The resolver found no specific match. Inject all capabilities as a safety net
            // so the main model can decide (the second phase was triggered for a reason).
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

    /// <summary>
    /// Tier 2: Makes a lightweight AI call (no chat history) to identify which MCP
    /// connections have capabilities that can handle the user's request.
    /// </summary>
    private async Task<HashSet<string>> ResolveCapabilitiesAsync(
        IntentProcessingContext context,
        List<McpServerCapabilities> allCapabilities,
        CancellationToken cancellationToken)
    {
        try
        {
            var chatClient = await CreateChatClientAsync(context);

            if (chatClient is null)
            {
                return null;
            }

            var systemPrompt = BuildCapabilityResolutionPrompt(allCapabilities);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, context.Prompt),
            };

            var options = new ChatOptions
            {
                Temperature = 0.0f,
                MaxOutputTokens = 300,
            };

            var response = await chatClient.GetResponseAsync(messages, options, cancellationToken);
            var responseText = response?.Text;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return null;
            }

            return ParseResolverResponse(responseText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP capability resolution AI call failed, falling back to all capabilities.");

            return null;
        }
    }

    private async Task<IChatClient> CreateChatClientAsync(IntentProcessingContext context)
    {
        var providerName = context.Source;

        if (string.IsNullOrEmpty(providerName)
            || !_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            return null;
        }

        var connectionName = context.CompletionContext?.ConnectionName;

        if (string.IsNullOrEmpty(connectionName))
        {
            connectionName = provider.DefaultConnectionName;
        }

        if (string.IsNullOrEmpty(connectionName)
            || !provider.Connections.TryGetValue(connectionName, out var connection))
        {
            return null;
        }

        var deploymentName = connection.GetDefaultDeploymentName(throwException: false);

        if (string.IsNullOrEmpty(deploymentName))
        {
            return null;
        }

        return await _aiClientFactory.CreateChatClientAsync(providerName, connectionName, deploymentName);
    }

    private static string BuildCapabilityResolutionPrompt(List<McpServerCapabilities> allCapabilities)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a capability matcher. Given the following list of available MCP server capabilities, determine which connections (if any) can handle the user's request.");
        sb.AppendLine();
        sb.AppendLine("Available capabilities:");

        foreach (var capabilities in allCapabilities)
        {
            sb.AppendLine();
            sb.Append("Connection \"");
            sb.Append(capabilities.ConnectionId);
            sb.Append("\" (");
            sb.Append(capabilities.ConnectionDisplayText);
            sb.AppendLine("):");

            AppendCapabilityList(sb, "Tools", capabilities.Tools);
            AppendCapabilityList(sb, "Resources", capabilities.Resources);
            AppendCapabilityList(sb, "Prompts", capabilities.Prompts);
        }

        sb.AppendLine();
        sb.AppendLine("Respond ONLY with a JSON object. If capabilities match the user's request:");
        sb.AppendLine("""{"matches":["connectionId1","connectionId2"]}""");
        sb.AppendLine("If no capabilities match:");
        sb.AppendLine("""{"matches":[]}""");

        return sb.ToString();
    }

    private static void AppendCapabilityList(StringBuilder sb, string label, IReadOnlyList<McpServerCapability> items)
    {
        if (items is null || items.Count == 0)
        {
            return;
        }

        sb.Append("  ");
        sb.Append(label);
        sb.AppendLine(":");

        foreach (var item in items)
        {
            sb.Append("    - ");
            sb.Append(item.Name);

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                sb.Append(": ");
                sb.Append(item.Description);
            }

            sb.AppendLine();
        }
    }

    private static HashSet<string> ParseResolverResponse(string responseText)
    {
        // Extract JSON from the response (handle markdown code fences).
        var jsonStart = responseText.IndexOf('{');
        var jsonEnd = responseText.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            return null;
        }

        var json = responseText[jsonStart..(jsonEnd + 1)];

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("matches", out var matches)
                && matches.ValueKind == JsonValueKind.Array)
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in matches.EnumerateArray())
                {
                    var value = item.GetString();

                    if (!string.IsNullOrEmpty(value))
                    {
                        result.Add(value);
                    }
                }

                return result;
            }
        }
        catch (JsonException)
        {
            // Malformed response, fall through.
        }

        return null;
    }
}
