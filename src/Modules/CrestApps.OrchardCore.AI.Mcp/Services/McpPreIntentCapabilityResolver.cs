using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// MCP implementation of <see cref="IPreIntentCapabilityResolver"/> that delegates
/// to the embedding-based <see cref="IMcpCapabilityResolver"/> and adapts the result
/// into the generic <see cref="PreIntentResolutionContext"/>.
/// </summary>
internal sealed class McpPreIntentCapabilityResolver : IPreIntentCapabilityResolver
{
    private readonly IMcpCapabilityResolver _mcpResolver;

    public McpPreIntentCapabilityResolver(IMcpCapabilityResolver mcpResolver)
    {
        _mcpResolver = mcpResolver;
    }

    public async Task<PreIntentResolutionContext> ResolveAsync(
        PromptRoutingContext routingContext,
        AICompletionContext completionContext,
        CancellationToken cancellationToken = default)
    {
        var mcpConnectionIds = completionContext?.McpConnectionIds;

        if (mcpConnectionIds is null || mcpConnectionIds.Length == 0)
        {
            return PreIntentResolutionContext.Empty;
        }

        var result = await _mcpResolver.ResolveAsync(
            routingContext.Prompt,
            routingContext.Source,
            routingContext.ConnectionName ?? completionContext.ConnectionName,
            mcpConnectionIds,
            cancellationToken);

        if (result is null || !result.HasRelevantCapabilities)
        {
            return PreIntentResolutionContext.Empty;
        }

        // Adapt MCP-specific candidates to generic CapabilitySummary.
        var summaries = new List<CapabilitySummary>(result.Candidates.Count);

        foreach (var candidate in result.Candidates)
        {
            summaries.Add(new CapabilitySummary
            {
                SourceId = candidate.ConnectionId,
                SourceDisplayText = candidate.ConnectionDisplayText,
                Name = candidate.CapabilityName,
                Description = candidate.CapabilityDescription,
                Score = candidate.SimilarityScore,
            });
        }

        return new PreIntentResolutionContext(summaries);
    }
}
