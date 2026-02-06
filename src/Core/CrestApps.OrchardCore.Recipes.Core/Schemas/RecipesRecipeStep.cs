using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "recipes" recipe step — executes other named recipes.
/// </summary>
public sealed class RecipesRecipeStep : IRecipeStep
{
    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    private JsonSchema _cached;

    public RecipesRecipeStep(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        IShellFeaturesManager shellFeaturesManager)
    {
        _recipeHarvesters = recipeHarvesters;
        _shellFeaturesManager = shellFeaturesManager;
    }

    public string Name => "recipes";

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var features = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(_recipeHarvesters, features);

        var recipeNames = recipes
            .Select(r => r.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _cached = CreateSchema(recipeNames);

        return _cached;
    }

    private static JsonSchema CreateSchema(string[] recipeNames)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("recipes")),
                ("Values", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("executionid", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(recipeNames)))
                        .Required("executionid", "name")
                        .AdditionalProperties(true))))
            .Required("name", "Values")
            .AdditionalProperties(true)
            .Build();
    }

    private static async Task<IEnumerable<RecipeDescriptor>> GetRecipesAsync(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        IEnumerable<IFeatureInfo> features)
    {
        var recipeCollections = await Task.WhenAll(recipeHarvesters.Select(x => x.HarvestRecipesAsync()));

        return recipeCollections.SelectMany(x => x)
            .Where(r => !r.IsSetupRecipe
                && features.Any(f => r.BasePath != null
                    && f.Extension?.SubPath != null
                    && r.BasePath.Contains(f.Extension.SubPath, StringComparison.OrdinalIgnoreCase)));
    }
}
