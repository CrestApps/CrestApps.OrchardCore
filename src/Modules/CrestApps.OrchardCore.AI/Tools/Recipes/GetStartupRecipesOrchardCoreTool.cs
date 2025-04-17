using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Tools.Recipes;

public sealed class GetStartupRecipesOrchardCoreTool : AIFunction
{
    public const string TheName = "getStartupRecipes";

    private readonly DocumentJsonSerializerOptions _options;
    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;
    private readonly ShellSettings _shellSettings;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public GetStartupRecipesOrchardCoreTool(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        ShellSettings shellSettings,
        IOptions<DocumentJsonSerializerOptions> options,
        IShellFeaturesManager shellFeaturesManager)
    {
        _recipeHarvesters = recipeHarvesters;
        _shellSettings = shellSettings;
        _options = options.Value;
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

    public override string Description => "Retrieves a list of predefined startup recipes that can be executed when the tenant is first set up.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!_shellSettings.IsDefaultShell())
        {
            return "This function is not supported in this tenant. It can only be used in the default tenant.";
        }

        var features = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(features);

        return JsonSerializer.Serialize(recipes.Select(x => new
        {
            x.Name,
            x.DisplayName,
            x.Description,
        }), _options.SerializerOptions);
    }

    private async Task<IEnumerable<RecipeDescriptor>> GetRecipesAsync(IEnumerable<IFeatureInfo> features)
    {
        var recipeCollections = await Task.WhenAll(_recipeHarvesters.Select(x => x.HarvestRecipesAsync()));
        var recipes = recipeCollections.SelectMany(x => x)
            .Where(r => r.IsSetupRecipe &&
                features.Any(f => r.BasePath != null && f.Extension?.SubPath != null && r.BasePath.Contains(f.Extension.SubPath, StringComparison.OrdinalIgnoreCase)));

        return recipes;
    }
}
