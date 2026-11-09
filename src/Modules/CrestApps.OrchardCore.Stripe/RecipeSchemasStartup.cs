using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Stripe.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Stripe;

/// <summary>
/// Describes the Stripe settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, StripeSettingsSchema>();
    }
}

/// <summary>
/// Describes the Stripe workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<StripePaymentReceivedEventSchema>();
        services.AddWorkflowActivitySchema<StripeSubscriptionCreatedEventSchema>();
        services.AddWorkflowActivitySchema<StripePaymentIntentSucceededEventSchema>();
        services.AddWorkflowActivitySchema<StripePaymentFailedEventSchema>();
        services.AddWorkflowActivitySchema<StripePaymentCanceledEventSchema>();
        services.AddWorkflowActivitySchema<StripePaymentRefundedEventSchema>();
        services.AddWorkflowActivitySchema<StripeDisputeCreatedEventSchema>();
        services.AddWorkflowActivitySchema<StripeRequestFailedEventSchema>();
    }
}
