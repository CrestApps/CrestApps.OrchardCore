using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.Recipes;

public sealed class ExecuteStartupRecipesTool : AIFunction
{
    public const string TheName = "executeNonStartupRecipe";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "recipeName": {
              "type": "string",
              "description": "The name of the non-startup recipe to execute."
            }
          },
          "required": ["recipeName"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Executes a non-startup recipe and applies all instructions defined within the recipe.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var recipeHarvesters = arguments.Services.GetRequiredService<IEnumerable<IRecipeHarvester>>();
        var recipeEnvironmentProviders = arguments.Services.GetRequiredService<IEnumerable<IRecipeEnvironmentProvider>>();
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var recipeExecutor = arguments.Services.GetRequiredService<IRecipeExecutor>();
        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();
        var shellHost = arguments.Services.GetRequiredService<IShellHost>();
        var shellSettings = arguments.Services.GetRequiredService<ShellSettings>();
        var logger = arguments.Services.GetRequiredService<ILogger<ExecuteStartupRecipesTool>>();

        if (!arguments.TryGetFirstString("recipeName", out var recipeName))
        {
            return "Unable to find a recipeName argument in the function arguments.";
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageRecipes))
        {
            return "You do not have permission to execute a recipe.";
        }

        var features = await shellFeaturesManager.GetAvailableFeaturesAsync();
        var recipes = await GetRecipesAsync(recipeHarvesters, features);

        var recipe = recipes.FirstOrDefault(c => c.Name == recipeName);

        if (recipe is null)
        {
            return "Unable to find a recipe that match the given recipe name.";
        }

        var environment = new Dictionary<string, object>();
        await recipeEnvironmentProviders.OrderBy(x => x.Order).InvokeAsync((provider, env) => provider.PopulateEnvironmentAsync(env), environment, logger);

        try
        {
            var executionId = Guid.NewGuid().ToString("n");

            await recipeExecutor.ExecuteAsync(executionId, recipe, environment, cancellationToken);

            await shellHost.ReleaseShellContextAsync(shellSettings);

            return $"The recipe '{recipe.DisplayName}' has been run successfully";
        }
        catch (RecipeExecutionException e)
        {
            logger.LogError(e, "Unable to import a recipe file.");

            return $"The recipe '{recipe.DisplayName}' failed to run due to the following errors: {string.Join(' ', e.StepResult.Errors)}";
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to import a recipe file.");

            return $"Unexpected error occurred while running the '{recipe.DisplayName}' recipe.";
        }
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
