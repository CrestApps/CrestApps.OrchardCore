using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Shipping.Deployments;
using CrestApps.OrchardCore.Shipping.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping;

/// <summary>
/// Describes the shipping zones recipe step and the shipping settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ShippingZonesRecipeStepSchema>();
        services.AddDeploymentStepSchema<ShippingZonesDeploymentStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, ShippingSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, CarrierProviderSettingsSchema>();
    }
}

/// <summary>
/// Describes the EasyPost settings to CrestApps Recipes.
/// </summary>
[Feature(ShippingConstants.Features.EasyPost)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class EasyPostRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, EasyPostSettingsSchema>();
    }
}

/// <summary>
/// Describes the shipment workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<ShipmentCreatedEventSchema>();
        services.AddWorkflowActivitySchema<ShipmentShippedEventSchema>();
        services.AddWorkflowActivitySchema<ShipmentDeliveredEventSchema>();
        services.AddWorkflowActivitySchema<ShipmentReturnedEventSchema>();
    }
}
