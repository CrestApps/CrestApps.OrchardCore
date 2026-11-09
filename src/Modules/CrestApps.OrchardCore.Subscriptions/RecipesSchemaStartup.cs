using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Recipes;
using CrestApps.OrchardCore.Subscriptions.Schemas;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Registers recipe schema contributors for the subscriptions feature.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, SubscriptionPartSchemaDefinition>();
        services.AddScoped<IContentSchemaDefinition, SubscriptionSummaryPartSchemaDefinition>();
        services.AddScoped<ISiteSettingsSchemaDefinition, SubscriptionSettingsSchema>();
    }
}

/// <summary>
/// Registers the schema of the roles given to new subscribers when the roles feature is enabled.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Roles)]
public sealed class RolesRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<ISiteSettingsSchemaDefinition, SubscriptionRoleSettingsSchema>();
}

/// <summary>
/// Registers the schemas of the subscription workflow events.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<SubscriptionStartedEventSchema>()
            .AddWorkflowActivitySchema<SubscriptionRenewedEventSchema>()
            .AddWorkflowActivitySchema<SubscriptionPastDueEventSchema>()
            .AddWorkflowActivitySchema<SubscriptionCanceledEventSchema>()
            .AddWorkflowActivitySchema<SubscriptionExpiredEventSchema>();
    }
}

/// <summary>
/// Registers the schema of the installment plan settings.
/// </summary>
[Feature(SubscriptionConstants.Features.Installments)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class InstallmentsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<ISiteSettingsSchemaDefinition, InstallmentPlanSettingsSchema>();
}
