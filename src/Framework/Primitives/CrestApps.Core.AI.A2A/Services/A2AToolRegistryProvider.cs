using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.A2A.Services;

/// <summary>
/// Provides tool registry entries for agents from remote A2A connections.
/// Each agent exposed by a connected A2A host becomes a tool entry that the orchestrator can invoke.
/// </summary>
internal sealed class A2AToolRegistryProvider : IToolRegistryProvider
{
    private readonly ICatalog<A2AConnection> _connectionStore;
    private readonly IA2AAgentCardCacheService _agentCardCache;
    private readonly ILogger _logger;

    public A2AToolRegistryProvider(
        ICatalog<A2AConnection> connectionStore,
        IA2AAgentCardCacheService agentCardCache,
        ILogger<A2AToolRegistryProvider> logger)
    {
        _connectionStore = connectionStore;
        _agentCardCache = agentCardCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionIds = context?.A2AConnectionIds;

        if (connectionIds is null || connectionIds.Length == 0)
        {
            return [];
        }

        var entries = new List<ToolRegistryEntry>();

        foreach (var connectionId in connectionIds)
        {
            var connection = await _connectionStore.FindByIdAsync(connectionId);

            if (connection is null || string.IsNullOrWhiteSpace(connection.Endpoint))
            {
                continue;
            }

            try
            {
                var agentCard = await _agentCardCache.GetAgentCardAsync(connectionId, connection, cancellationToken);

                if (agentCard?.Skills is null)
                {
                    continue;
                }

                foreach (var skill in agentCard.Skills)
                {
                    var skillName = SanitizeToolName(skill.Id ?? skill.Name ?? connection.DisplayText);
                    var capturedConnectionId = connectionId;

                    entries.Add(new ToolRegistryEntry
                    {
                        Id = $"a2a:{connectionId}:{skillName}",
                        Name = skillName,
                        Description = skill.Description ?? agentCard.Description,
                        Source = ToolRegistryEntrySource.A2AAgent,
                        SourceId = connectionId,
                        CreateAsync = _ => ValueTask.FromResult<AITool>(
                            new A2AAgentProxyTool(skillName, skill.Description ?? agentCard.Description, connection.Endpoint, capturedConnectionId)),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load agent card for A2A connection '{ConnectionId}'.", connectionId);
            }
        }

        return entries;
    }

    private static string SanitizeToolName(string name)
    {
        // Tool names must be valid identifiers. Replace invalid characters.
        var sanitized = new char[name.Length];

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            sanitized[i] = char.IsLetterOrDigit(c) || c == '_' ? c : '_';
        }

        return new string(sanitized);
    }
}
