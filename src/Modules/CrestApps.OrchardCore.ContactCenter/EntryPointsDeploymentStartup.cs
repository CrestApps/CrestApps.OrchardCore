using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the deployment steps that export the entry points owned by the entry points feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.EntryPoints)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class EntryPointsDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterEntryPointDeploymentSource, ContactCenterEntryPointDeploymentStep>();
    }
}
