using CrestApps.OrchardCore.Checkout.Deployments;
using CrestApps.OrchardCore.Checkout.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Describes the checkout recipe step, deployment step and settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, CouponsRecipeStepSchema>();
        services.AddDeploymentStepSchema<CouponsDeploymentStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, CheckoutSettingsSchema>();
    }
}
