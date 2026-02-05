using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles recipe-schema:// URI resources by providing JSON schema definitions for recipe steps.
/// The path portion of the URI is the recipe step name.
/// For example, recipe-schema://{itemId}/feature returns the schema for the "feature" step.
/// </summary>
public sealed class RecipeSchemaResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "recipe-schema";

    private readonly IEnumerable<IRecipeStep> _recipeSteps;
    private readonly ILogger _logger;

    public RecipeSchemaResourceTypeHandler(
        IEnumerable<IRecipeStep> recipeSteps,
        ILogger<RecipeSchemaResourceTypeHandler> logger)
        : base(TypeName)
    {
        _recipeSteps = recipeSteps;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, McpResourceUri resourceUri, CancellationToken cancellationToken)
    {
        var stepName = resourceUri.Path;

        if (string.IsNullOrEmpty(stepName))
        {
            return CreateErrorResult(resource.Resource.Uri, "Recipe step name is required in the URI path.");
        }

        _logger.LogDebug("Reading recipe-schema resource for step: {StepName}", stepName);

        var recipeStep = _recipeSteps.FirstOrDefault(s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

        if (recipeStep is null)
        {
            return CreateErrorResult(resource.Resource.Uri, $"Recipe step not found: {stepName}. Available steps: {string.Join(", ", _recipeSteps.Select(s => s.Name))}");
        }

        var schema = await recipeStep.GetSchemaAsync();

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = "application/schema+json",
                    Text = schema.ToString(),
                }
            ]
        };
    }
}
