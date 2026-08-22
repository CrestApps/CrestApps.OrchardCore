using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Schemas;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Registers the tenant onboarding recipe schema contributor when both the tenant onboarding
/// and recipes features are enabled.
/// </summary>
[Feature(SubscriptionConstants.Features.TenantOnboarding)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class TenantOnboardingRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, TenantOnboardingPartSchemaDefinition>();
    }
}
