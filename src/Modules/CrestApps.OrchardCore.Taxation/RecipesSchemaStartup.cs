using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Schemas;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Registers recipe schema contributors for the taxation feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TaxCategoryRecipeStep>();
        services.AddScoped<IRecipeStep, TaxTypeRecipeStep>();
        services.AddScoped<IRecipeStep, TaxJurisdictionRecipeStep>();
        services.AddScoped<IRecipeStep, TaxRuleRecipeStep>();

        services.AddScoped<IContentSchemaDefinition, TaxationPartSchemaDefinition>();
    }
}
