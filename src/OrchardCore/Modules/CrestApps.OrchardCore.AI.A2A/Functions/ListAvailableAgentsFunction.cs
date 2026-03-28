using System.Text.Json;
using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A.Services;
using CrestApps.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.A2A.Functions;

/// <summary>
/// An AI system function that lists all available agents, including both local AI Agent profiles
/// and remote agents from connected A2A hosts.
/// </summary>
internal sealed class ListAvailableAgentsFunction : AIFunction
{
    public const string TheName = "listAvailableAgents";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description
        => "Lists all available AI agents, including local agents and remote agents from connected A2A hosts. Returns their names, descriptions, and capabilities.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>()
        {
            ["Strict"] = false,
        };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<ListAvailableAgentsFunction>>();

        var agents = new List<object>();

        try
        {
            var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();
            var localProfiles = await profileManager.GetAsync(AIProfileType.Agent);

            if (localProfiles is not null)
            {
                foreach (var profile in localProfiles)
                {
                    agents.Add(new
                    {
                        name = profile.DisplayText ?? profile.Name,
                        id = profile.Name,
                        description = profile.Description,
                        source = "local",
                    });
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list local agent profiles.");
        }

        try
        {
            var connectionStore = arguments.Services.GetRequiredService<ICatalog<A2AConnection>>();
            var agentCardCache = arguments.Services.GetRequiredService<IA2AAgentCardCacheService>();
            var connections = await connectionStore.GetAllAsync();

            foreach (var connection in connections)
            {
                if (string.IsNullOrWhiteSpace(connection.Endpoint))
                {
                    continue;
                }

                try
                {
                    var agentCard = await agentCardCache.GetAgentCardAsync(
                        connection.ItemId, connection, cancellationToken);

                    if (agentCard?.Skills is null)
                    {
                        continue;
                    }

                    foreach (var skill in agentCard.Skills)
                    {
                        agents.Add(new
                        {
                            name = skill.Name ?? skill.Id,
                            id = skill.Id,
                            description = skill.Description ?? agentCard.Description,
                            source = "remote",
                            host = connection.DisplayText,
                            tags = skill.Tags,
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to fetch agent card from A2A connection '{ConnectionId}'.",
                        connection.ItemId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list remote A2A agents.");
        }

        return agents.Count > 0
            ? JsonSerializer.Serialize(agents)
            : "No agents are currently available.";
    }
}
