using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class RemoveContentTypeDefinitionsTool : AIFunction
{
    public const string TheName = "removeContentTypeDefinition";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly RecipeExecutionService _recipeExecutionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public RemoveContentTypeDefinitionsTool(
        IContentDefinitionManager contentDefinitionManager,
        RecipeExecutionService recipeExecutionService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _recipeExecutionService = recipeExecutionService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Removes the content type definition for a given content type.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.EditContentTypes))
        {
            return "You do not have permission to edit content definitions.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(name);

        if (typeDefinition is null)
        {
            return
                $"""
                Unable to find a type definition that match the name: {name}.
                Here are the available content types that can be removed:
                {JsonSerializer.Serialize((await _contentDefinitionManager.ListTypeDefinitionsAsync()).Select(x => x.Name), JsonHelpers.ContentDefinitionSerializerOptions)}
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

        if (await _recipeExecutionService.ExecuteRecipeAsync(data))
        {
            return $"The content type {name} was removed successfully";
        }

        return "Unable to remove the content type definition.";
    }
}
