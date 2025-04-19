using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agents.Recipes;

public sealed class ExecuteStartupRecipesTool : AIFunction
{
    public const string TheName = "executeNonStartupRecipe";

    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;
    private readonly IEnumerable<IRecipeEnvironmentProvider> _recipeEnvironmentProviders;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRecipeExecutor _recipeExecutor;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    public ExecuteStartupRecipesTool(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        IEnumerable<IRecipeEnvironmentProvider> recipeEnvironmentProviders,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IRecipeExecutor recipeExecutor,
        IShellFeaturesManager shellFeaturesManager,
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<ExecuteStartupRecipesTool> logger)
    {
        _recipeHarvesters = recipeHarvesters;
        _recipeEnvironmentProviders = recipeEnvironmentProviders;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _recipeExecutor = recipeExecutor;
        _shellFeaturesManager = shellFeaturesManager;
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _logger = logger;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "recipeName": {
                        "type": "string",
                        "description": "The name of the non-startup recipe to execute."
                    }
                },
                "additionalProperties": false,
                "required": ["recipeName"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Executes a non-startup recipe and applies all instructions defined within the recipe.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue("recipeName", out var data))
        {
            return "Unable to find a recipeName argument in the function arguments.";
        }

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageRecipes))
        {
            return "You do not have permission to execute a recipe.";
        }

        var recipeName = data is JsonElement jsonElement
            ? jsonElement.GetString()
            : data?.ToString();

        if (string.IsNullOrEmpty(recipeName))
        {
            return "No value was given as the recipeName argument.";
        }

        var features = await _shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(features);

        var recipe = recipes.FirstOrDefault(c => c.Name == recipeName);

        if (recipe is null)
        {
            return "Unable to find a recipe that match the given recipe name.";
        }

        var environment = new Dictionary<string, object>();
        await _recipeEnvironmentProviders.OrderBy(x => x.Order).InvokeAsync((provider, env) => provider.PopulateEnvironmentAsync(env), environment, _logger);

        try
        {
            var executionId = Guid.NewGuid().ToString("n");

            await _recipeExecutor.ExecuteAsync(executionId, recipe, environment, cancellationToken);

            await _shellHost.ReleaseShellContextAsync(_shellSettings);

            return $"The recipe '{recipe.DisplayName}' has been run successfully";
        }
        catch (RecipeExecutionException e)
        {
            _logger.LogError(e, "Unable to import a recipe file.");

            return $"The recipe '{recipe.DisplayName}' failed to run due to the following errors: {string.Join(' ', e.StepResult.Errors)}";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to import a recipe file.");

            return $"Unexpected error occurred while running the '{recipe.DisplayName}' recipe.";
        }
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
