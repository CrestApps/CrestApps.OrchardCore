using System.Text.Json;
using CrestApps.AI;
using CrestApps.AI.Extensions;
using CrestApps.OrchardCore.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.AI.Models;
using CrestApps.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.A2A.Functions;

/// <summary>
/// An AI system function that uses keyword and semantic matching to find the best agents
/// capable of handling a given task description. Searches both local agent profiles and
/// remote A2A agents.
/// </summary>
internal sealed class FindAgentForTaskFunction : AIFunction
{
    public const string TheName = "findAgentForTask";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "taskDescription": {
              "type": "string",
              "description": "A description of the task to find an agent for."
            },
            "maxResults": {
              "type": "integer",
              "description": "Maximum number of agents to return. Defaults to 5."
            }
          },
          "required": ["taskDescription"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description
        => "Finds the most relevant AI agents for a specific task using keyword and semantic matching. Searches both local agents and remote A2A agents.";

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

        var logger = arguments.Services.GetRequiredService<ILogger<FindAgentForTaskFunction>>();

        if (!arguments.TryGetFirstString("taskDescription", out var taskDescription)
            || string.IsNullOrWhiteSpace(taskDescription))
        {
            return "A task description is required to find matching agents.";
        }

        var maxResults = 5;

        if (arguments.TryGetValue("maxResults", out var maxResultsObj))
        {
            if (maxResultsObj is int intVal)
            {
                maxResults = intVal;
            }
            else if (maxResultsObj is JsonElement element && element.TryGetInt32(out var parsed))
            {
                maxResults = parsed;
            }
        }

        var tokenizer = arguments.Services.GetRequiredService<ITextTokenizer>();
        var queryTokens = tokenizer.Tokenize(taskDescription);

        if (queryTokens.Count == 0)
        {
            return "Unable to analyze the task description. Please provide more details.";
        }

        var scoredAgents = new List<(object Agent, double Score)>();

        try
        {
            var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();
            var localProfiles = await profileManager.GetAsync(AIProfileType.Agent);

            if (localProfiles is not null)
            {
                foreach (var profile in localProfiles)
                {
                    var text = (profile.DisplayText ?? profile.Name) + " " + profile.Description;
                    var score = ScoreRelevance(queryTokens, tokenizer.Tokenize(text));

                    scoredAgents.Add((new
                    {
                        name = profile.DisplayText ?? profile.Name,
                        id = profile.Name,
                        description = profile.Description,
                        source = "local",
                        relevanceScore = Math.Round(score, 3),
                    }, score));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to search local agent profiles.");
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
                        var text = (skill.Name ?? skill.Id) + " " + (skill.Description ?? agentCard.Description);

                        if (skill.Tags is not null)
                        {
                            text += " " + string.Join(" ", skill.Tags);
                        }

                        var score = ScoreRelevance(queryTokens, tokenizer.Tokenize(text));

                        scoredAgents.Add((new
                        {
                            name = skill.Name ?? skill.Id,
                            id = skill.Id,
                            description = skill.Description ?? agentCard.Description,
                            source = "remote",
                            host = connection.DisplayText,
                            tags = skill.Tags,
                            relevanceScore = Math.Round(score, 3),
                        }, score));
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
            logger.LogWarning(ex, "Failed to search remote A2A agents.");
        }

        var results = scoredAgents
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Take(maxResults)
            .Select(s => s.Agent)
            .ToList();

        return results.Count > 0
            ? JsonSerializer.Serialize(results)
            : "No agents were found matching the given task description.";
    }

    private static double ScoreRelevance(HashSet<string> queryTokens, HashSet<string> targetTokens)
    {
        if (queryTokens.Count == 0 || targetTokens.Count == 0)
        {
            return 0;
        }

        var matchCount = 0;

        foreach (var token in queryTokens)
        {
            if (targetTokens.Contains(token))
            {
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            return 0;
        }

        var forwardScore = (double)matchCount / queryTokens.Count;
        var reverseScore = (double)matchCount / targetTokens.Count;

        return Math.Max(forwardScore, reverseScore);
    }
}
