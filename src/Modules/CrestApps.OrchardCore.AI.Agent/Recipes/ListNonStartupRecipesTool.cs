using System.Text.Json;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ListNonStartupRecipesTool : AIFunction
{
    public const string TheName = "listNonStartupRecipes";

    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public ListNonStartupRecipesTool(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        IShellFeaturesManager shellFeaturesManager)
    {
        _recipeHarvesters = recipeHarvesters;
        _shellFeaturesManager = shellFeaturesManager;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "additionalProperties": false,
                "required": []
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Retrieves a list of predefined recipes that can be executed manually and are not designated to run at application startup.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var features = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(features);

        return JsonSerializer.Serialize(recipes.Select(x => x.AsAIObject()));
    }

    private async Task<IEnumerable<RecipeDescriptor>> GetRecipesAsync(IEnumerable<IFeatureInfo> features)
    {
        var recipeCollections = await Task.WhenAll(_recipeHarvesters.Select(x => x.HarvestRecipesAsync()));
        var recipes = recipeCollections.SelectMany(x => x)
            .Where(r => !r.IsSetupRecipe &&
                (r.Tags == null || !r.Tags.Contains("hidden", StringComparer.InvariantCultureIgnoreCase)) &&
                features.Any(f => r.BasePath != null && f.Extension?.SubPath != null && r.BasePath.Contains(f.Extension.SubPath, StringComparison.OrdinalIgnoreCase)));

        return recipes;
    }
}
