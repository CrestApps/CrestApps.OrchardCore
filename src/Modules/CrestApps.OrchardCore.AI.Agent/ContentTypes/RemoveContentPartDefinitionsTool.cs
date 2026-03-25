using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class RemoveContentPartDefinitionsTool : AIFunction
{
    public const string TheName = "removeContentPartDefinition";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "name": {
              "type": "string",
              "description": "The name of the content part for which to remove the definitions."
            }
          },
          "required": ["name"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Removes the content part definition for a given content part.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<RemoveContentPartDefinitionsTool>>();
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();
        var recipeExecutionService = arguments.Services.GetRequiredService<RecipeExecutionService>();

        if (!arguments.TryGetFirstString("name", out var name))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'name' argument.", TheName);

            return "Unable to find a name argument in the function arguments.";
        }

        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(name);

        if (partDefinition is null)
        {
            logger.LogWarning("AI tool '{ToolName}' could not find a part definition matching the name '{ContentPart}'.", TheName, name);

            return
                $"""
                Unable to find a part definition that match the name: {name}.
                Here are the available part that can be removed:
                {JsonSerializer.Serialize((await contentDefinitionManager.ListPartDefinitionsAsync()).Select(x => x.Name), JsonHelpers.ContentDefinitionSerializerOptions)}
                """;
        }

        var data = JsonNode.Parse(
            $$"""
            {
              "steps": [
                {
                  "name": "DeleteContentDefinition",
                  "ContentParts": [
                    "{{name}}"
                  ]
                }
              ]
            }
            """);

        if (await recipeExecutionService.ExecuteRecipeAsync(data))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
            }

            return $"The content part {name} was removed successfully";
        }

        logger.LogWarning("AI tool '{ToolName}' failed to remove content part definition '{ContentPart}'.", TheName, name);

        return "Unable to remove the content part definition.";
    }
}
