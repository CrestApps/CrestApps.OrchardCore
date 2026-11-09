using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Recipes;
using CrestApps.OrchardCore.Subscriptions.Schemas;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Registers the tenant onboarding recipe schema contributor when both the tenant onboarding
/// and recipes features are enabled.
/// </summary>
[Feature(SubscriptionConstants.Features.Tenants)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TenantOnboardingRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, TenantOnboardingPartSchemaDefinition>();
        services.AddScoped<ISiteSettingsSchemaDefinition, SubscriptionOnboardingSettingsSchema>();
    }
}

/// <summary>
/// Registers the schemas of the subscribed tenant workflow events when workflows are enabled.
/// </summary>
[Feature(SubscriptionConstants.Features.Tenants)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class TenantOnboardingWorkflowRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<SubscribedTenantSetupSucceededEventSchema>()
            .AddWorkflowActivitySchema<SubscribedTenantFailedSetupEventSchema>();
    }
}
