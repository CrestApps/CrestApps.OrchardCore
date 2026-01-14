using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ListStartupRecipesTool : AIFunction
{
    public const string TheName = "listStartupRecipes";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Retrieves a list of predefined startup recipes that can be executed when the tenant is first set up.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var recipeHarvesters = arguments.Services.GetRequiredService<IEnumerable<IRecipeHarvester>>();
        var shellSettings = arguments.Services.GetRequiredService<ShellSettings>();
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();

        if (!shellSettings.IsDefaultShell())
        {
            return "This function is not supported in this tenant. It can only be used in the default tenant.";
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageRecipes))
        {
            return "You do not have permission to execute a recipe.";
        }

        var features = await shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(recipeHarvesters, features);

        return JsonSerializer.Serialize(recipes.Select(x => x.AsAIObject()));
    }

    private static async Task<IEnumerable<RecipeDescriptor>> GetRecipesAsync(IEnumerable<IRecipeHarvester> recipeHarvesters, IEnumerable<IFeatureInfo> features)
    {
        var recipeCollections = await Task.WhenAll(recipeHarvesters.Select(x => x.HarvestRecipesAsync()));
        var recipes = recipeCollections.SelectMany(x => x)
            .Where(r => r.IsSetupRecipe &&
                features.Any(f => r.BasePath != null && f.Extension?.SubPath != null && r.BasePath.Contains(f.Extension.SubPath, StringComparison.OrdinalIgnoreCase)));

        return recipes;
    }
}
