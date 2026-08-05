using CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the editors for the Contact Center configuration deployment steps when Orchard Deployment is enabled.
/// </summary>
[Feature(ContactCenterConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ContactCenterDeploymentAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<DeploymentStep, AgentStateReasonCodeDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterSkillDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterAgentEntitlementDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterQueueGroupDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterBusinessHoursCalendarDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterQueueDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterEntryPointDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterDialerProfileDeploymentStepDisplayDriver>();
    }
}
