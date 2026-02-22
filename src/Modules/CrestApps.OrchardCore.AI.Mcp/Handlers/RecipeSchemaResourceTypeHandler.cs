using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles recipe-schema resources by providing the full JSON schema definition for recipes.
/// No variables are needed; the resource returns the complete recipe schema with all steps.
/// </summary>
public sealed class RecipeSchemaResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "recipe-schema";

    private readonly RecipeSchemaService _recipeSchemaService;
    private readonly ILogger _logger;

    public RecipeSchemaResourceTypeHandler(
        RecipeSchemaService recipeSchemaService,
        ILogger<RecipeSchemaResourceTypeHandler> logger)
        : base(TypeName)
    {
        _recipeSchemaService = recipeSchemaService;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Returning full recipe schema.");

        var recipeSchema = await _recipeSchemaService.GetRecipeSchemaAsync();

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = "application/schema+json",
                    Text = JsonSerializer.Serialize(recipeSchema),
                }
            ]
        };
    }
}
