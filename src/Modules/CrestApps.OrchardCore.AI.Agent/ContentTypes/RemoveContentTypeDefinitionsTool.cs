using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Represents the remove content type definitions tool.
/// </summary>
public sealed class RemoveContentTypeDefinitionsTool : AIFunction
{
    public const string TheName = "removeContentTypeDefinition";

    private const string DeleteContentDefinitionStepName = "DeleteContentDefinition";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The name of the content type for which to remove the definitions."
        }
      },
      "required": [
        "name"
      ],
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Removes the content type definition for a given content type.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<RemoveContentTypeDefinitionsTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();

        if (!arguments.TryGetFirstString("name", out var name))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'name' argument.", TheName);

            return "Unable to find a name argument in the function arguments.";
        }

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(name);

        if (typeDefinition is null)
        {
            logger.LogWarning("AI tool '{ToolName}' could not find a type definition matching the name '{ContentType}'.", TheName, name);

            return
                $"""
                Unable to find a type definition that match the name: {name}.
                Here are the available content types that can be removed:
                {JsonSerializer.Serialize((await contentDefinitionManager.ListTypeDefinitionsAsync()).Select(x => x.Name), JsonHelpers.ContentDefinitionSerializerOptions)}

                """;
        }

        // The step runs in this scope. Importing it as a recipe ran it in a scope and connection of its own, which waits
        // for the write lock the chat request holds (on SQLite, "database is locked").
        var errors = await RecipeStepRunner.RunAsync(arguments.Services, DeleteContentDefinitionStepName, new JsonObject
        {
            ["name"] = DeleteContentDefinitionStepName,
            ["ContentTypes"] = new JsonArray(typeDefinition.Name),
        });

        if (errors.Count == 0)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
            }

            return $"The content type {name} was removed successfully";
        }

        logger.LogWarning("AI tool '{ToolName}' failed to remove content type definition '{ContentType}'.", TheName, name);

        return $"Unable to remove the content type definition: {string.Join(" ", errors)}";
    }
}
