using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles recipe-schema:// URI resources by providing JSON schema definitions for recipe steps and recipes.
/// Supported URI path patterns:
///   - recipe              → full recipe schema with all steps
///   - recipe/{recipe-name} → the JSON content of a specific recipe found by name
///   - step/{step-name}     → the JSON schema for a specific recipe step
/// </summary>
public sealed class RecipeSchemaResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "recipe-schema";

    private readonly RecipeSchemaService _recipeSchemaService;
    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly ILogger _logger;

    public RecipeSchemaResourceTypeHandler(
        RecipeSchemaService recipeSchemaService,
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        IShellFeaturesManager shellFeaturesManager,
        ILogger<RecipeSchemaResourceTypeHandler> logger)
        : base(TypeName)
    {
        _recipeSchemaService = recipeSchemaService;
        _recipeHarvesters = recipeHarvesters;
        _shellFeaturesManager = shellFeaturesManager;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, McpResourceUri resourceUri, CancellationToken cancellationToken)
    {
        var path = resourceUri.Path;

        // Parse the path into a segment and an optional value.
        // e.g., "step/feature" → segment = "step", value = "feature"
        // e.g., "recipe" → segment = "recipe", value = null
        string segment;
        string value;

        var slashIndex = path?.IndexOf('/') ?? -1;

        if (slashIndex > 0)
        {
            segment = path[..slashIndex];
            value = path[(slashIndex + 1)..];
        }
        else
        {
            segment = path;
            value = null;
        }

        if (string.Equals(segment, "step", StringComparison.OrdinalIgnoreCase))
        {
            return await GetStepSchemaResultAsync(resource, value);
        }

        if (string.Equals(segment, "recipe", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(value))
            {
                return await GetFullRecipeSchemaResultAsync(resource);
            }

            return await GetRecipeByNameResultAsync(resource, value);
        }

        return CreateErrorResult(resource.Resource.Uri, $"Unsupported path: '{path}'. Use 'recipe', 'recipe/{{recipe-name}}', or 'step/{{step-name}}'.");
    }

    private async Task<ReadResourceResult> GetFullRecipeSchemaResultAsync(McpResource resource)
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
                    Text = recipeSchema.ToString(),
                }
            ]
        };
    }

    private async Task<ReadResourceResult> GetStepSchemaResultAsync(McpResource resource, string stepName)
    {
        if (string.IsNullOrEmpty(stepName))
        {
            return CreateErrorResult(resource.Resource.Uri, "Step name is required. Use 'step/{step-name}'.");
        }

        _logger.LogDebug("Reading recipe-schema resource for step: {StepName}", stepName);

        var stepSchema = await _recipeSchemaService.GetStepSchemaAsync(stepName);

        if (stepSchema is null)
        {
            return CreateErrorResult(resource.Resource.Uri, $"Recipe step not found: '{stepName}'. Available steps: {string.Join(", ", _recipeSchemaService.GetStepNames())}");
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = "application/schema+json",
                    Text = stepSchema.ToString(),
                }
            ]
        };
    }

    private async Task<ReadResourceResult> GetRecipeByNameResultAsync(McpResource resource, string recipeName)
    {
        _logger.LogDebug("Reading recipe-schema resource for recipe: {RecipeName}", recipeName);

        var features = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipe = await FindRecipeByNameAsync(recipeName, features);

        if (recipe is null)
        {
            return CreateErrorResult(resource.Resource.Uri, $"Recipe not found: '{recipeName}'.");
        }

        var json = await ReadRecipeFileAsync(recipe);

        if (json is null)
        {
            return CreateErrorResult(resource.Resource.Uri, $"Unable to read recipe file for '{recipeName}'.");
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = "application/json",
                    Text = json,
                }
            ]
        };
    }

    private async Task<RecipeDescriptor> FindRecipeByNameAsync(string recipeName, IEnumerable<IFeatureInfo> features)
    {
        var recipeCollections = await Task.WhenAll(_recipeHarvesters.Select(x => x.HarvestRecipesAsync()));

        return recipeCollections
            .SelectMany(x => x)
            .Where(r => features.Any(f =>
                r.BasePath != null &&
                f.Extension?.SubPath != null &&
                r.BasePath.Contains(f.Extension.SubPath, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(r => string.Equals(r.Name, recipeName, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> ReadRecipeFileAsync(RecipeDescriptor recipe)
    {
        if (recipe.FileProvider is null || string.IsNullOrEmpty(recipe.BasePath))
        {
            return null;
        }

        var fileInfo = recipe.FileProvider.GetFileInfo(recipe.BasePath);

        if (!fileInfo.Exists)
        {
            return null;
        }

        using var stream = fileInfo.CreateReadStream();
        using var document = await JsonDocument.ParseAsync(stream);

        return JsonSerializer.Serialize(document, JOptions.Indented);
    }
}
