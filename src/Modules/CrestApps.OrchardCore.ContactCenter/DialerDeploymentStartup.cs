using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the deployment steps that export the dialer profiles owned by the dialer feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DialerDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterDialerProfileDeploymentSource, ContactCenterDialerProfileDeploymentStep>();
    }
}
