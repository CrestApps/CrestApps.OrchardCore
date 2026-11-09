using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Schemas;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Registers recipe schema contributors for the taxation feature.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TaxCategoryRecipeStep>();
        services.AddScoped<IRecipeStep, TaxTypeRecipeStep>();
        services.AddScoped<IRecipeStep, TaxJurisdictionRecipeStep>();
        services.AddScoped<IRecipeStep, TaxRuleRecipeStep>();
        services.AddScoped<IRecipeStep, TaxTableRecipeStep>();
        services.AddScoped<IRecipeStep, MerchantTaxRegistrationRecipeStepSchema>();
        services.AddScoped<IRecipeStep, ExemptionCertificateRecipeStepSchema>();
        services.AddDeploymentStepSchema<MerchantTaxRegistrationDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ExemptionCertificateDeploymentStepSchema>();

        services.AddScoped<IContentSchemaDefinition, TaxationPartSchemaDefinition>();
        services.AddScoped<ISiteSettingsSchemaDefinition, TaxOriginSettingsSchema>();
    }
}

/// <summary>
/// Registers the schema of the TaxJar settings.
/// </summary>
[Feature(TaxationConstants.Feature.TaxJar)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TaxJarRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<ISiteSettingsSchemaDefinition, TaxJarSettingsSchema>();
}
