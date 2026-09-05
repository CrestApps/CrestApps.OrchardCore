using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the optional Agent Entitlements feature: it replaces the permissive default entitlement policy with an
/// enforcing one and adds the entitlement administration screen. When this feature is disabled, the permissive
/// default stands and any agent may sign in to any queue or campaign with no per-agent setup.
/// </summary>
[Feature(ContactCenterConstants.Feature.AgentEntitlements)]
public sealed class AgentEntitlementsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Override the permissive default (registered by the Agents feature) so sign-in and the soft-phone picker
        // limit an agent to the queues and campaigns granted on their profile.
        services.Replace(ServiceDescriptor.Scoped<IAgentEntitlementPolicy, EnforcingAgentEntitlementPolicy>());
        services.AddNavigationProvider<ContactCenterAgentEntitlementsAdminMenu>();
    }
}

/// <summary>
/// Registers the deployment source and step editor that export agent entitlements.
/// </summary>
[Feature(ContactCenterConstants.Feature.AgentEntitlements)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class AgentEntitlementsDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterAgentEntitlementDeploymentSource, ContactCenterAgentEntitlementDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterAgentEntitlementDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the recipe step that imports agent entitlements.
/// </summary>
[Feature(ContactCenterConstants.Feature.AgentEntitlements)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class AgentEntitlementsRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterAgentEntitlementStep>();
    }
}
