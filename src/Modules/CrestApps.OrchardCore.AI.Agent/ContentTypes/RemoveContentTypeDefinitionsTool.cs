using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class RemoveContentTypeDefinitionsTool : AIFunction
{
    public const string TheName = "removeContentTypeDefinition";

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
          "required": ["name"],
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

        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();
        var recipeExecutionService = arguments.Services.GetRequiredService<RecipeExecutionService>();

        if (!await arguments.IsAuthorizedAsync(OrchardCorePermissions.EditContentTypes))
        {
            return "You do not have permission to edit content definitions.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(name);

        if (typeDefinition is null)
        {
            return
                $"""
                Unable to find a type definition that match the name: {name}.
                Here are the available content types that can be removed:
                {JsonSerializer.Serialize((await contentDefinitionManager.ListTypeDefinitionsAsync()).Select(x => x.Name), JsonHelpers.ContentDefinitionSerializerOptions)}
                """;
        }

        var data = JsonNode.Parse(
            $$"""
            {
              "steps": [
                {
                  "name": "DeleteContentDefinition",
                  "ContentTypes": [
                    "{{name}}"
                  ]
                }
              ]
            }
            """);

        if (await recipeExecutionService.ExecuteRecipeAsync(data))
        {
            return $"The content type {name} was removed successfully";
        }

        return "Unable to remove the content type definition.";
    }
}
