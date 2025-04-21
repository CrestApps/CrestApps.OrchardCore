using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agents.Recipes;

public sealed class ListStartupRecipesTool : AIFunction
{
    public const string TheName = "listStartupRecipes";

    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;
    private readonly ShellSettings _shellSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public ListStartupRecipesTool(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        ShellSettings shellSettings,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellFeaturesManager shellFeaturesManager)
    {
        _recipeHarvesters = recipeHarvesters;
        _shellSettings = shellSettings;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
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

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageRecipes))
        {
            return "You do not have permission to execute a recipe.";
        }

        var features = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(features);

        return JsonSerializer.Serialize(recipes.Select(x => x.AsAIObject()));
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
