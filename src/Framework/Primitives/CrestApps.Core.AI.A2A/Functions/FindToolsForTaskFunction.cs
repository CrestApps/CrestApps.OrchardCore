using System.Text.Json;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.A2A.Functions;

/// <summary>
/// An AI system function that uses keyword and semantic search to find the most relevant
/// AI tools for a given task. Delegates to <see cref="IToolRegistry.SearchAsync"/> for
/// consistent scoring with the orchestrator's tool scoping logic.
/// </summary>
internal sealed class FindToolsForTaskFunction : AIFunction
{
    public const string TheName = "findToolsForTask";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "taskDescription": {
          "type": "string",
          "description": "A description of the task to find relevant tools for."
        },
        "maxResults": {
          "type": "integer",
          "description": "Maximum number of tools to return. Defaults to 10."
        }
      },
      "required": ["taskDescription"],
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description
        => "Finds the most relevant AI tools for a specific task using keyword and semantic matching. Returns tools ranked by relevance.";

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

        var logger = arguments.Services.GetRequiredService<ILogger<FindToolsForTaskFunction>>();

        if (!arguments.TryGetFirstString("taskDescription", out var taskDescription)
            || string.IsNullOrWhiteSpace(taskDescription))
        {
            return "A task description is required to find matching tools.";
        }

        var maxResults = 10;

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

        try
        {
            var connectionStore = arguments.Services.GetRequiredService<ICatalog<A2AConnection>>();
            var toolRegistry = arguments.Services.GetRequiredService<IToolRegistry>();

            var context = new AICompletionContext
            {
                A2AConnectionIds = (await connectionStore.GetAllAsync())
                    .Where(connection => !string.IsNullOrWhiteSpace(connection.Endpoint))
                    .Select(connection => connection.ItemId)
                    .ToArray(),
            };

            var results = await toolRegistry.SearchAsync(
                taskDescription,
                maxResults,
                context,
                cancellationToken);

            if (results is null || results.Count == 0)
            {
                return "No tools were found matching the given task description.";
            }

            var tools = results.Select(r => new
            {
                name = r.Name,
                description = r.Description,
                source = r.Source.ToString(),
            }).ToList();

            return JsonSerializer.Serialize(tools);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to search for tools.");

            return "An error occurred while searching for tools.";
        }
    }
}
