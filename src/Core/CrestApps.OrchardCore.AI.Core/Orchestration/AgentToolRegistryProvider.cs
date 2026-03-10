using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Provides agent profiles as tool entries in the tool registry.
/// Each AI profile with <see cref="AIProfileType.Agent"/> type is exposed
/// as a <see cref="ToolRegistryEntry"/> so the AI model can invoke agents as tools.
/// </summary>
/// <remarks>
/// This provider is registered as scoped to ensure it always reads the latest
/// agent profiles without requiring a tenant restart.
/// </remarks>
internal sealed class AgentToolRegistryProvider : IToolRegistryProvider
{
    private readonly IAIProfileManager _profileManager;

    public AgentToolRegistryProvider(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var agents = await _profileManager.GetAsync(AIProfileType.Agent);
        var entries = new List<ToolRegistryEntry>();

        if (agents is null)
        {
            return entries;
        }

        var requestedAgents = context?.AgentNames;

        foreach (var agent in agents)
        {
            if (string.IsNullOrEmpty(agent.Name) || string.IsNullOrEmpty(agent.Description))
            {
                continue;
            }

            var agentMetadata = agent.As<AgentMetadata>();
            var isSystemAgent = agentMetadata?.IsSystemAgent == true;

            // Include system agents automatically, or include user-selected agents.
            if (!isSystemAgent && requestedAgents is not { Length: > 0 })
            {
                continue;
            }

            if (!isSystemAgent && !requestedAgents.Contains(agent.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var agentName = agent.Name;
            entries.Add(new ToolRegistryEntry
            {
                Id = $"agent:{agentName}",
                Name = agentName,
                Description = agent.Description,
                Source = ToolRegistryEntrySource.Agent,
                SourceId = agent.ItemId,
                CreateAsync = (sp) => ValueTask.FromResult<AITool>(new AgentProxyTool(agentName, agent.Description)),
            });
        }

        return entries;
    }
}
