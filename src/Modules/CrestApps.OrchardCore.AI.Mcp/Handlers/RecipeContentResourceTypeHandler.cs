using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Handles recipe-content resources by returning the JSON content of a specific recipe by name.
/// Supported variable: {recipeName} - the name of the recipe to retrieve.
/// </summary>
public sealed class RecipeContentResourceTypeHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "recipe";

    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly ILogger _logger;

    public RecipeContentResourceTypeHandler(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        IShellFeaturesManager shellFeaturesManager,
        ILogger<RecipeContentResourceTypeHandler> logger)
        : base(TypeName)
    {
        _recipeHarvesters = recipeHarvesters;
        _shellFeaturesManager = shellFeaturesManager;
        _logger = logger;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        variables.TryGetValue("recipeName", out var recipeName);

        if (string.IsNullOrEmpty(recipeName))
        {
            return CreateErrorResult(resource.Resource.Uri, "Recipe name is required. Include {recipeName} in the URI pattern.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reading recipe content for recipe: {RecipeName}", recipeName);
        }

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
