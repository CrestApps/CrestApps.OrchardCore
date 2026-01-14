using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ListNonStartupRecipesTool : AIFunction
{
    public const string TheName = "listNonStartupRecipes";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Retrieves a list of predefined recipes that can be executed manually and are not designated to run at application startup.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services, nameof(arguments.Services));

        var recipeHarvesters = arguments.Services.GetRequiredService<IEnumerable<IRecipeHarvester>>();
        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();

        var features = await shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(recipeHarvesters, features);

        return JsonSerializer.Serialize(recipes.Select(x => x.AsAIObject()));
    }

    private static async Task<IEnumerable<RecipeDescriptor>> GetRecipesAsync(IEnumerable<IRecipeHarvester> recipeHarvesters, IEnumerable<IFeatureInfo> features)
    {
        var recipeCollections = await Task.WhenAll(recipeHarvesters.Select(x => x.HarvestRecipesAsync()));
        var recipes = recipeCollections.SelectMany(x => x)
            .Where(r => !r.IsSetupRecipe &&
                (r.Tags == null || !r.Tags.Contains("hidden", StringComparer.InvariantCultureIgnoreCase)) &&
                features.Any(f => r.BasePath != null && f.Extension?.SubPath != null && r.BasePath.Contains(f.Extension.SubPath, StringComparison.OrdinalIgnoreCase)));

        return recipes;
    }
}
