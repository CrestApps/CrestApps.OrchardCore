using CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the editors for the Contact Center configuration deployment steps. The steps themselves are headless, so
/// a tenant that runs without an administration surface can still be exported by a script; only the screens that add
/// the steps to a plan need the administration feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Admin)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ContactCenterDeploymentAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<DeploymentStep, AgentStateReasonCodeDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterSkillDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterQueueGroupDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterBusinessHoursCalendarDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterQueueDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterEntryPointDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterDialerProfileDeploymentStepDisplayDriver>();
    }
}
