using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders;

/// <summary>
/// Describes the order settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, OrderSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, OrderEmailTemplateSettingsSchema>();
    }
}

/// <summary>
/// Describes the return settings to CrestApps Recipes.
/// </summary>
[Feature(OrderConstants.Features.Returns)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class ReturnsRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ReturnSettingsSchema>();
    }
}

/// <summary>
/// Describes the order workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<OrderPaidEventSchema>();
        services.AddWorkflowActivitySchema<OrderCanceledEventSchema>();
        services.AddWorkflowActivitySchema<OrderRefundedEventSchema>();
    }
}
