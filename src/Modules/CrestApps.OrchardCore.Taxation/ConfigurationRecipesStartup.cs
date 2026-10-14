using CrestApps.OrchardCore.Taxation.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Registers the recipe steps that import taxation configuration.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TaxCategoryStep>();
        services.AddRecipeExecutionStep<TaxTypeStep>();
        services.AddRecipeExecutionStep<TaxJurisdictionStep>();
        services.AddRecipeExecutionStep<TaxRuleStep>();
        services.AddRecipeExecutionStep<TaxTableStep>();
    }
}
