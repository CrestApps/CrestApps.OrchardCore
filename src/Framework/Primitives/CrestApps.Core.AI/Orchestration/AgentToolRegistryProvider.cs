using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.AI.Orchestration;

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
            var isAlwaysAvailable = agentMetadata?.Availability == AgentAvailability.AlwaysAvailable;

            // Always-available agents are automatically included in every request.
            // On-demand agents are only included if explicitly selected via AgentNames.
            if (!isAlwaysAvailable && requestedAgents is not { Length: > 0 })
            {
                continue;
            }

            if (!isAlwaysAvailable && !requestedAgents.Contains(agent.Name, StringComparer.OrdinalIgnoreCase))
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
