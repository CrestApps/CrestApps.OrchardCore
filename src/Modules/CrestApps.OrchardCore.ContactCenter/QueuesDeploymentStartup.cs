using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the deployment steps that export the routing configuration owned by the queues feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class QueuesDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterSkillDeploymentSource, ContactCenterSkillDeploymentStep>();
        services.AddDeployment<ContactCenterQueueGroupDeploymentSource, ContactCenterQueueGroupDeploymentStep>();
        services.AddDeployment<ContactCenterBusinessHoursCalendarDeploymentSource, ContactCenterBusinessHoursCalendarDeploymentStep>();
        services.AddDeployment<ContactCenterQueueDeploymentSource, ContactCenterQueueDeploymentStep>();
    }
}
